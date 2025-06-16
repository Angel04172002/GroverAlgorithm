# Built-in modules
import math
from flask import Flask, request, jsonify
from io import BytesIO
import base64

# Imports from Qiskit
from qiskit import transpile
from qiskit import QuantumCircuit
from qiskit.circuit.library import GroverOperator, MCMTGate, ZGate
from qiskit.visualization import plot_distribution
from qiskit.transpiler.preset_passmanagers import generate_preset_pass_manager

# Imports from Qiskit Runtime
from qiskit_ibm_runtime import QiskitRuntimeService, Sampler as RuntimeSampler
from qiskit.providers import QiskitBackendNotFoundError
from qiskit_aer import AerSimulator

import matplotlib.pyplot as plt

app = Flask('GroverInQiskit')

# Connect to IBM Quantum service
service = QiskitRuntimeService(channel="ibm_quantum", token="775ff0be8bc19236558d43f2f44d69fb83765f23f051946b405334892d0b7e74ef001e85ed1ea83558f6995a62d8ec77af720eabbb7c384b00a64ee5d2136347")
# Use a backend you have access to (example: 'ibm_brisbane')
backend = service.backend("ibm_brisbane")

def grover_oracle(marked_states):
    if not isinstance(marked_states, list):
        marked_states = [marked_states]

    num_qubits = len(marked_states[0])
    qc = QuantumCircuit(num_qubits)
    for target in marked_states:
        rev_target = target[::-1]
        zero_inds = [ind for ind in range(num_qubits) if rev_target[ind] == "0"]
        if zero_inds:
            qc.x(zero_inds)
        qc.compose(MCMTGate(ZGate(), num_qubits - 1, 1), inplace=True)
        if zero_inds:
            qc.x(zero_inds)
    return qc

@app.route("/api/run-grover", methods=["POST"])
def run_grover():
    data = request.get_json()

    marked_states = data.get("marked_states", [])
    shots = data.get("shots", 10000)
    optimization_level = data.get("optimization_level", 3)
    num_iterations = data.get("num_iterations")
    return_histogram = data.get("return_histogram", True)

    if not marked_states:
        return jsonify({"error": "No marked_states provided"}), 400

    # Choose backend
    selected_backend = backend  # Always use IBM backend for cloud execution

    # Oracle and Grover operator
    oracle = grover_oracle(marked_states)
    grover_op = GroverOperator(oracle)

    if num_iterations is None:
        num_iterations = math.floor(
            math.pi / (4 * math.asin(math.sqrt(len(marked_states) / 2 ** grover_op.num_qubits)))
        )

    qc = QuantumCircuit(grover_op.num_qubits)
    qc.h(range(grover_op.num_qubits))
    qc.compose(grover_op.power(num_iterations), inplace=True)
    qc.measure_all()

    # Transpile circuit for backend
    qc = transpile(qc, backend=selected_backend, optimization_level=optimization_level)

    # Run within session using Runtime Sampler
    sampler = RuntimeSampler(mode=selected_backend)

    sampler.options.default_shots = shots
    result = sampler.run([qc], shots = shots).result()
    counts = result[0].data.meas.get_counts()
 

    counts = result[0].data.meas.get_counts()

    # Plot distribution
    image_base64 = None
    if return_histogram:
        fig = plot_distribution(counts)
        buf = BytesIO()
        fig.savefig(buf, format="png")
        buf.seek(0)
        image_base64 = base64.b64encode(buf.read()).decode("utf-8")
        buf.close()

    return jsonify({
        "marked_states": marked_states,
        "shots": shots,
        "iterations": num_iterations,
        "backend": selected_backend.name,
        "counts": counts,
        "histogram_base64": image_base64 if return_histogram else None
    })

if __name__ == "__main__":
    app.run(debug=True)

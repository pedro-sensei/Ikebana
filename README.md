Ikebana: Alpha Blue 🌸

Ikebana (Alpha Blue) is a digital board game developed as a Final Degree Project (TFG) for the Computer Engineering program at the Universitat Oberta de Catalunya (UOC).

The project focuses on the development of a sophisticated digital board game—inspired by the mechanics of the popular game Azul—and the implementation of various Artificial Intelligence (AI) architectures to provide a challenging and varied single-player experience.

🎮 The Game: Ikebana

Ikebana is a tile-placement drafting game where players compete to create the most beautiful floral wall.

Drafting Phase: Players pick flower tiles of a single color from various baskets (factories) or the central pool.

Planning Phase: Tiles are placed on pattern lines (bamboo stairs) to prepare them for planting.

Planting Phase: Completed lines are moved to the 5x5 Garden Wall, scoring points based on adjacency and completed sets.

Endgame: The game concludes when a player completes a horizontal row on their wall.

🧠 Artificial Intelligence Lab

The core of this project is the "Alpha Blue" engine, which implements several types of AI opponents:

1. Traditional Heuristics & Search

Random: A baseline opponent for testing.

Min-Max (Alpha-Beta Pruning): A tree-search algorithm that evaluates future states to find optimal moves. Includes configurable depth and time limits.

Optimizer: A heuristic-based brain whose weights were fine-tuned using Evolutionary Algorithms.

2. Reinforcement Learning (ML-Agents)

Using Unity ML-Agents and Proximal Policy Optimization (PPO), a neural network was trained to "learn" the game through millions of iterations.

IkebanaAgentV2: An advanced agent capable of complex drafting strategies and defensive play.

🛠️ Technical Stack

Engine: Unity 2022.3+

Language: C#

AI Framework: Unity ML-Agents (TensorFlow/PyTorch)

Data Persistence: ScriptableObjects for opponent profiles and game configurations.

Architecture: Decoupled game logic to allow seamless switching between Human and AI players.

📁 Project Structure

/Assets/Scripts/Core: Game loop, rules engine, and scoring logic.

/Assets/Scripts/AI: Implementations of Min-Max, ML-Agents wrappers, and Heuristic brains.

/Assets/ML-Agents: Training configurations and exported .onnx neural network models.

/Assets/Prefabs: UI elements and tile assets.

🚀 Getting Started

Clone the repository:

git clone [https://github.com/pedro-sensei/Ikebana.git](https://github.com/pedro-sensei/Ikebana.git)


Open in Unity: Use Unity Hub to open the project folder. Ensure you have the ML-Agents package installed via the Package Manager.

Play: Open the START scene and press Play. You can configure opponent types (Human vs. MinMax vs. ML-Agent) in the setup menu.

OR

You can download the zip with the latest release to test without unity.

📜 Academic Context

This project was developed by Pedro Sánchez Vázquez as a TFG under the supervision of Manel Hidalgo Agraz. The full thesis covers:

Formalization of board game mechanics into software logic.

Analysis of state-space complexity.

Comparison of performance between search-based and learning-based AI.

Optimization of heuristics via genetic-inspired algorithms.

Note: This project is licensed under Creative Commons BY-NC-ND 3.0 Spain.

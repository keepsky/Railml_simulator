# RailML DES Simulator

RailML DES Simulator is a **Discrete Event Simulation (DES)** application developed in **C# .NET 10 (WPF)**.
It simulates train movements and signaling logic based on railway infrastructure defined in **RailML 2.5** format.

## Features

- **RailML 2.5 Support**: Parses track topology, signals, and switches from RailML files.
- **Discrete Event Simulation**: Time-based event engine for precise train movement handling.
- **High-Performance Rendering**: Visualizes tracks, trains, and signals using **SkiaSharp**.
- **Interlocking Logic**: Basic signaling and switch control systems.
- **Collision Detection**: Real-time safety monitoring for train collisions.

## Tech Stack

- **Language**: C# 13 (.NET 10)
- **Framework**: WPF (Windows Presentation Foundation)
- **Graphics**: SkiaSharp & SkiaSharp.Views.WPF
- **Architecture**: MVVM Pattern

## Project Structure

- `Railml.Sim.Core`: Core simulation logic (DES engine, SimObjects, RailML models).
- `Railml.Sim.UI`: WPF Application for visualization and user interaction.

## How to Run

1. Open `RailmlSimulator.slnx` in Visual Studio 2022 (or later).
2. Build the solution (requires .NET 10 SDK).
3. Run the `Railml.Sim.UI` project.
4. Click **Load RailML** to open a `.railml` or `.xml` file.
5. Click **Start** to begin the simulation.
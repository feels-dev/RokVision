# Roadmap

This document outlines the development trajectory for RoK Vision, detailing completed milestones and planned features.

---

## ✅ Completed Milestones (v0.1.0 - v0.5.1)

This section lists the core features that are currently implemented, tested, and available in the latest release.

- **[x] Enterprise API Architecture (JSON v2.0)**: A complete architectural overhaul providing a standardized, resilient, and enterprise-grade API response across all endpoints. Includes spatial coordinates for automation, full traceability, and a structured telemetry pipeline.
- **[x] Governor Profile Extraction**: Full cognitive analysis of player profiles, including Power, Kill Points, Alliance, and Civilization.
- **[x] Battle/War Reports (PvP & PvE)**: Comprehensive extraction of battle log data with mathematical consistency validation and PVE-specific logic.
- **[x] Kingdom Map Intelligence**: A hybrid engine combining **YOLOv8 object detection** and OCR to identify cities, shields, and coordinates from map screenshots.
- **[x] Alliance Rally Intelligence**: Advanced analysis of single and multi-page rally screens, with participant extraction and a logical inference engine for troop types.
- **[x] Inventory Intelligence (AP & XP)**: Topological grid solver for reading dense item inventories, including Action Points and Experience books, powered by a visual color engine to identify item rarity.

---

## 🎯 Current Focus: v0.6.0 - The Core Vision Engine Overhaul

This is the next major milestone. The primary goal is to **replace complex, brittle heuristics with specialized AI models**, dramatically increasing precision, speed, and maintainability. The Enterprise API from v0.5.1 was built specifically to support this transition.

- **[ ] Transition from Heuristics to AI-Powered Detection**:
  - Phase out most of the spatial- and text-based logic (e.g., "find the number to the right of the word 'Power'").
  - Implement a suite of custom-trained, lightweight **YOLOv8 models** to visually identify the exact bounding box of each UI element.

- **[ ] Micro-Model Specialization**:
  - Instead of one large model, we will train several small, hyper-focused models for different game contexts (e.g., a model for Governor UI, a model for Report metrics, a model for map icons), ensuring maximum speed and accuracy.

- **[ ] Code Refactoring for AI Consumption**:
  - Simplify the C# "Cognitive Neurons" to act as parsers and validators for the data found inside the regions provided by YOLO, making the codebase cleaner and more robust.

---

## 🚀 Planned Feature Expansions (Post-v0.6.0)

Once the core Vision Engine is in place, we can rapidly expand support for new game screens and languages.

- **[ ] Dynamic Event & Ranking Extractor**:
  - A generic, AI-powered engine capable of understanding and parsing any tabular data screen, such as in-game events (Mightiest Governor, Zenith of Power), KvK rankings, and more.
  - The goal is to eliminate the need to build a new, specific extractor for every new event the game introduces, making the system future-proof.

- **[ ] Dedicated Endpoints for Core Rankings**:
  - While the dynamic extractor will be versatile, we will provide clean, dedicated endpoints for the most common use-cases, powered by the generic table parser.
    - **[ ] Global Leaderboards**: Power, Kill Points, and Alliance.
    - **[ ] Alliance-Internal Rankings**: Member Power, Weekly Contributions, and Alliance Help statistics.

- **[ ] Full CJK Character Support**:
  - The transition to a YOLO-based detection engine in v0.6.0 is the key enabler. By visually identifying text regions first, we can then pass these specific areas to an OCR model trained on Chinese, Japanese, and Korean, making the system truly language-agnostic.

- **[ ] Auto-Detection of Screenshot Type**:
  - An AI classifier that automatically determines the type of screenshot sent to the API (e.g., Governor, Map, Report) and routes it to the correct orchestrator.

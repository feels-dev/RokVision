# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.5.1] - 2026-03-07

### Added
- **Enterprise API Standard (JSON v2.0)**: A complete overhaul of the API response structure across **ALL** endpoints (`/governor`, `/rally`, `/reports`, `/map`, `/ap`, `/xp`).
  - **Automation-Ready Spatial Data**: Every extracted field now returns a `spatial` object containing both **Absolute** (pixel) and **Normalized** (0.0 to 1.0) coordinates. This allows developers to build overlays and automation bots that are **100% resolution-independent**.
  - **Granular Traceability**: Fields now report their extraction `source`, detailing exactly which logic pipeline was responsible (e.g., `"detector": "IdNeuron_StrictLabel"` vs `"strategy": "Magnifier_Rescue"`).
  - **Structured Telemetry**: Replaced flat text logs with a structured `executionTrace` object, enabling programmatic auditing of the decision-making process.
  - **Correlation IDs**: Every request now generates and tracks a unique UUID (`correlationId`) for end-to-end debugging between the C# Orchestrator and Python Engine.

- **Interactables Framework**: Added the `interactables` dictionary to the response schema. While currently populated by OCR heuristics, this structure is the **foundation for the upcoming v0.6.0**, which will fill this section with non-text UI elements (buttons, icons) detected by YOLOv8.

### Changed
- **Architectural Unification**: Refactored `ReportOrchestrator`, `RallyOrchestrator`, `MapOrchestrator`, etc to share the same rigorous context validation and error handling logic as the Governor module.
- **Battle Report Integrity**: Fixed a critical logic gap where numerical metrics (e.g., "Total Units", "Dead") were correctly parsed but lacked spatial reference data. The system now retains 100% of the bounding box lineage for every number.
- **Confidence Scoring**: Moved from static confidence assignment to a dynamic, weighted scoring system based on the actual OCR blocks used for each specific field.

### Deprecated
- **Legacy Response Format**: The old flat JSON structure has been replaced by the new `DataEnvelope<T>` pattern. Clients should migrate to reading `data.businessSummary` for values and `data.extractedFields` for metadata.

---

## [0.5.0] - 2026-03-05

### Added
- **Alliance Rally Intelligence**: New endpoint `/api/rally/analyze` capable of reading complex War screens (both "War Details" and "Active Rallies" lists).
  - **Context-Aware Slicing**: The system automatically detects if the screenshot is a single rally detail or a list of active rallies and adjusts the scanning strategy accordingly.
  - **Participant Card Extraction**: Identifies and extracts individual rally participants, including their Name, Primary/Secondary Commanders, and marched troop count.
  - **Logical Inference Engine**: A specialized "Brain" component that deduces missing troop types by cross-referencing individual march counts with the rally's global unit totals (e.g., if the rally is 100% Infantry, all participants are inferred as Infantry).
  - **Geometric Auto-Repair**: Uses the `RallyTroopMagnifier` to mathematically reconstruct missing data (like troop counts) based on relative anchor positions (e.g., Commander Level labels).
  - **Multi-Image Stitching**: Supports scrolling screenshots to capture long lists of participants without duplicating data.

### Known Issues
- **Troop Tier Detection (Beta)**: The current heuristic for detecting Troop Tiers (T4 vs T5) based on icon color (`RallyTroopMagnifier`) is sensitive to the game's UI background colors. This may result in false positives (e.g., identifying T5 Gold borders when the background is dark blue).
  - **Workaround**: The `Logical Inference Engine` mitigates this by enforcing consistency with global rally totals where possible.
  - **Upcoming Fix**: We are actively refactoring the vision engine to replace pixel-math heuristics with **YOLOv8 Micro-Models** in v0.6.0 for 100% accurate visual recognition.

---

## [0.4.1] - 2026-03-04

### Added
- **Dynamic HUD Masking**: Introduced `DynamicHudLocator`, a smart cognitive component that analyzes the screenshot to identify UI elements (Chat, Resources, Menus) and creates a "forbidden zone" mask. This allows the Text Fallback engine to safely ignore text inside the interface while detecting cities in the open field with high precision.
- **Whitelist Override Logic**: Updated `CityNeuron` with a "Smart Blocklist" feature. It now correctly identifies and allows default player names (e.g., "Governor123456") even if the word "Governor" is in the global blocklist, solving a critical false-negative issue.
- **X-Ray Debugging**: Enhanced the visual debug system via API response.
  - **Audit Log Transparency**: The API response now includes a granular, step-by-step log of why each candidate was accepted or rejected (e.g., `BLOCKED_BY_VOCABULARY`, `LOW_LETTER_RATIO`).
  - **Raw Text Separation**: The debug output now cleanly separates the global OCR text from the targeted crop reads, making it easier to diagnose recognition issues without file system access.

### Changed
- **Map Strategy Refinement**: Switched the primary extraction strategy from `WhiteIsolation` to `MapLabel`. This new strategy uses adaptive contrast enhancement and color inversion (Black Text on White Background), which proved to be significantly more effective for reading thin city names against complex map textures.
- **Anchor Detection**: The `anchorsFound` list in the debug response is now correctly populated with UI elements found in the image, aiding in layout verification.

---

## [0.4.0] - 2026-03-03

### Added
- **Kingdom Map Intelligence**: New endpoint `/api/map/analyze` to extract all visible cities from a map screenshot.
  - **Hybrid Detection Engine**: A multi-layered approach combining three distinct strategies for maximum robustness:
    1.  **AI Visual Detection (YOLOv8)**: Uses a custom-trained model to find city nameplates and peace shields by their visual shape, immune to language or background clutter.
    2.  **Anchor-based Fallback (Shields)**: If a peace shield is detected without a corresponding nameplate, the system intelligently calculates the nameplate's expected location and performs a targeted OCR scan.
    3.  **Heuristic Text Analysis (OCR-first)**: As a final fallback, the system scans all text on the screen and uses a **Dynamic Safe Canvas** to filter out UI/Chat noise, identifying potential cities that the visual AI may have missed.
  - **Slicing Aided Hyper Inference (SAHI)**: For wide/high-resolution screenshots, the image is automatically split into overlapping slices before AI analysis. This preserves detail and allows the detection of very small, distant cities that would otherwise be lost in resizing.
  - **Magnifier Refinement Loop**: If a city is detected with low confidence or a missing alliance tag, the `MapMagnifier` is automatically triggered to perform a high-resolution re-scan of the specific area, significantly improving data accuracy.

### Changed
- **Vocabulary Expansion**: Added new sections to `RokVocabulary` (`TopUiAnchors`, `BottomUiAnchors`, `ChatKeywords`) to support the new Dynamic Canvas logic (coming soon).

### Known Issues
- **Beta Feature**: Map Analysis is highly effective for screenshots with up to ~1-3 visible cities. In extremely crowded scenarios (e.g., Ark of Osiris starting zones), detection accuracy for smaller, overlapping cities may decrease. Fine-tuning of the "Safe Canvas" and de-duplication logic is ongoing.

---

## [0.3.1] - 2026-01-31

### Added
- **Global Debug Mode**: Added optional `Debug: true` parameter to ALL API endpoints (`/governor`, `/reports`, `/ap`, `/xp`).
  - Enables rich output, including **Raw OCR Text** for all processed images.
  - Adds detailed **Timings** per processing step (`TotalOrchestration`, `PythonInitialRead`, `Classification`, `MagnifierBatchRepair`, etc.).
  - Logs detected **Anchors** (e.g., `PowerLabel`, `CivLabel`) for easy contextual validation.
  - Logs detailed status and success rate of **Magnifier/Batch Repair** attempts.
- **Instrumented Magnifier**: `GovernorMagnifier` and `WarMagnifier` now report their full execution state and success strategies to the `OcrAnalysisContext`.

### Changed
- **Unified Response Structure**: The API response has been standardized across **all** endpoints to the `RokResponse<T>` pattern.
  - **BREAKING CHANGE (Minor):** The `debug` field is now an object (`DebugInformationDto`) and is only present if `Debug: true` is requested. If `false`, it returns `null` or is omitted.
  - **Consistency:** All controllers (`GovernorController`, `ReportController`, `ActionPointsController`, `ExperienceController`) are refactored to use the unified audit context and response factory.
- **Improved Logging**: `OcrAnalysisContext` logic is enhanced with new `StartTimer`/`StopTimer` methods for precise measurement of all internal components.

---

## [0.3.0] - 2025-12-26

### Added
- **Inventory Intelligence (Action Points)**: New endpoint `/api/ap/analyze`. Supports **Multi-Image Scroll Merging** (consolidates lists across multiple screenshots) and automatic conflict resolution for duplicate items.
- **Inventory Intelligence (Experience)**: New endpoint `/api/xp/analyze`. Features a sophisticated **Topological Grid Solver** capable of associating values to icons in dense grids.
- **Visual Color Engine**: Python backend now performs **HSV/HLS Color Histogram Analysis** to distinguish item types by rarity (e.g., Green AP vs. Blue XP vs. Gold Books), ignoring irrelevant items like resources.
- **White Isolation Filter**: A specialized Computer Vision filter (`WhiteIsolation`) to extract white numbers from complex/bright backgrounds (Golden/Cyan icons) where standard binarization fails.
- **Multi-Shot Magnifier**: The Auto-Healing engine now uses a "Sniper" approach, capturing multiple geometric perspectives (Center, Right-Bias, Wide) to resolve small numbers hidden in corners.
- **Global Matchmaking Algorithm**: Implemented a "Best Fit" auction logic for grids to prevent items from "stealing" quantities from neighboring cells (Line of Sight protection).
- **Resolution Agnosticism**: All cognitive neurons now use **Relative Geometry** (based on Font Height), ensuring 100% accuracy on any resolution (720p to 4K) or UI scaling.
- **Sanity Checks**: Logic to detect and discard "Ghost Reads" (duplicate values inherited from neighbors) using spatial awareness.

### Changed
- **Python Backend**: Added `det=True` support for batch processing, allowing the OCR to find small text floating inside larger cropped regions.
- **Topology Graph**: Updated to support infinite canvas scaling, removing hardcoded references to 1080p/1920p.

---

## [0.2.0] - 2025-12-22

### Added
- **Battle Reports Module**: Complete support for PvP and PvE battle logs via `/api/reports/analyze`.
- **Warp Perspective Engine**: Python-based pre-processor that isolates and straightens the report paper, removing UI noise.
- **Dynamic Confidence Algorithm**: Accuracy scoring based on mathematical consistency (Total + Heal = Dead + Wounded + Remaining).
- **PvE Intelligence**: Specialized logic for Barbarians, Forts, and Marauders with NPC/Boss identification (e.g., Calvin, Kranitos) and damage percentage extraction.
- **Topological Graph Mapping**: Context-aware data extraction that understands spatial relationships between labels and values.
- **Transparent API Warnings**: System alerts for obscured names, unsupported CJK characters, or poorly framed screenshots.
- **NpcsVocabulary**: Dedicated JSON storage for boss and NPC detection.

### Changed
- **API Refactor**: Separated endpoints into specialized controllers: `/governor` and `/reports`.
- **Performance Optimization**: Optimized Python engine to return processed crops for auto-healing precision.
- **OCR Threshold**: Adjusted Python OCR confidence gate to allow the C# Brain to handle lower-confidence visual data.

---

## [0.1.0] - 2025-12-18

### Added
- Initial public release of RoK Vision API.
- Cognitive Orchestrator with specialized Neurons for Governor Profiles.
- The Magnifier: Auto-healing engine for low-confidence regions.
- High-performance OCR backend powered by PaddleOCR (PP-OCRv4).
- Smart image downscaling for 4K/2K screenshots.
- Fully containerized microservices architecture.
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
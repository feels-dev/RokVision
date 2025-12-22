# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
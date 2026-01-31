<div align="center">

# 🛡️ RoK Vision API

![Badge](https://img.shields.io/badge/.NET-9.0-purple?style=for-the-badge&logo=dotnet)
![Badge](https://img.shields.io/badge/Python-3.10-blue?style=for-the-badge&logo=python)
![Badge](https://img.shields.io/badge/PaddleOCR-v4-green?style=for-for-the-badge)
![Badge](https://img.shields.io/badge/Docker-Microservices-2496ED?style=for-for-the-badge&logo=docker)
![Badge](https://img.shields.io/badge/License-MIT-orange?style=for-for-the-badge)

**Next-Gen Cognitive OCR for Rise of Kingdoms**

<p align="center">
  <a href="#-key-features">Key Features</a> •
  <a href="#-architecture">Architecture</a> •
  <a href="#-getting-started">Getting Started</a> •
  <a href="#-api-usage">API Usage</a> •
  <a href="ROADMAP.md">Roadmap</a> •
  <a href="CONTRIBUTING.md">Contributing</a>
</p>

</div>

---

## 📖 Overview

**RoK Vision** is a high-performance **Cognitive OCR API** designed to transform *Rise of Kingdoms* screenshots into structured data. By combining **Deep Learning (PaddleOCR)** with a **Topological C# Orchestrator**, Vision understands the context of the screen, making it resolution-independent and extremely resilient to UI variations.

---

## 🚀 Key Features

*   **👤 Governor Profiles**
    Extracts ID, Name, Power, Kill Points, and Civilization from the profile screen with sub-second latency.
*   **⚔️ Battle Intelligence**
    Full analysis of PvP and PvE reports, including troop metrics, casualty rates, and boss identification.
*   **🎒 Inventory Intelligence**
    Reads complex inventory screens (Action Points & XP Books). Supports **Multi-Screenshot Merging** and uses **Color Detection** to distinguish items.
*   **✅ Standardized Output (NEW)**
    All endpoints now return a unified `RokResponse` structure with a complete **Audit Log** and detailed **Extraction Evidence** for every field.
*   **🔍 The Magnifier (Auto-Healing)**
    Automatic regional re-scanning with specialized digital filters (White Isolation, Inverted Binary) for low-confidence areas.
*   **🩺 Debug Mode (NEW)**
    Add `Debug: true` to any request to receive granular **Timings** per step, **Raw OCR Text**, and **Magnifier Attempt Logs** in the response.
*   **🌐 Multicultural Core**
    Optimized for Latin alphabets (EN, PT, ES, FR, DE) with smart detection of unsupported characters.

---

## 🏁 Getting Started

The easiest way to run RoK Vision is using Docker. It sets up the Neural Network environment and the API Gateway automatically.

👉 **[Read the Installation Guide](GETTING_STARTED.md)** to get up and running in 5 minutes.

---

## 🏗️ Architecture

The solution follows a distributed architecture: the **Muscle** (Python) handles the heavy AI computer vision, while the **Brain** (C#) manages the logical orchestration.

```
graph LR
    User["Client / Bot"] -->|"POST"| API["API Gateway (.NET 9)"]
    subgraph "The Brain (.NET 9)"
        API --> Orchestrator[Cognitive Orchestrator]
        Orchestrator --> Neurons[Specialized Neurons]
        Neurons --> Magnifier[The Magnifier]
    end
    subgraph "The Muscle (Python)"
        Orchestrator -->|"gRPC/HTTP"| OCR[PaddleOCR Engine]
    end
```

---

## 🔌 API Usage

### ⚙️ The Standard Response (`RokResponse<T>`)

Every successful response from the API is wrapped in the `RokResponse<T>` structure, ensuring a consistent contract across all endpoints.

| Field | Type | Description |
|---|---|---|
| `status.success` | `bool` | `true` if processing finished without critical error. |
| `status.overallConfidence`| `float` | Aggregated confidence score from 0 to 100. |
| `data.summary` | `T` (Model) | The final domain object (e.g., `GovernorProfile`, `ReportResult`) clean and ready to use. |
| `data.fields` | `Dictionary<string, FieldEvidenceDto>`| **Evidence:** Technical details, confidence, method, and bounding box for each extracted field. |
| `auditLog` | `List<string>` | Chronological history of decisions made by the OCR Orchestrator. |
| `debug` | `DebugInformationDto` | **OPTIONAL:** Detailed debug information, only present if `Debug: true` is sent in the request. |

### 1. Governor Profile
`POST /api/governor/analyze`  
*Requires: `IFormFile Image`, Optional: `int? DraftId`, `bool Debug`*

#### Sample `data.summary` (GovernorProfile)
```
{
  "id": 193397278,
  "name": "nan0z01",
  "allianceTag": "RE87",
  "power": 99999012,
  "killPoints": 2063935270,
  "civilization": "Germany"
}
```

### 2. Battle Reports
`POST /api/reports/analyze`  
*Requires: `IFormFile Image`, Optional: `bool Debug`*

#### Sample `data.summary` (ReportResult)
```
{
  "type": "SingleBattle_PVP",
  "attacker": { "governorName": "ml Feels", "totalUnits": 40342, "dead": 0, "severelyWounded": 19287 },
  "defender": { "governorName": "ITRIOSMANGAZi", "dead": 2451, "remaining": 75785 }
}
```

### 3. Action Points Inventory
`POST /api/ap/analyze`  
*Requires: `List<IFormFile> Images`, Optional: `bool Debug`*

#### Sample `data.summary` (ApInventoryData)
```
{
  "grandTotalAp": 338750,
  "currentBarValue": 875,
  "items": [
    {
      "name": "Basic Action Point Recovery",
      "apValue": 100,
      "quantity": 2086,
      "confidence": 99.5
    }
  ]
}
```

### 4. Experience Inventory (Tomes of Knowledge)
`POST /api/xp/analyze`  
*Requires: `List<IFormFile> Images`, Optional: `bool Debug`*

#### Sample `data.summary` (XpInventoryData)
```
{
  "totalXp": 182180300,
  "items": [
    {
      "itemId": "XP_50000",
      "unitValue": 50000,
      "quantity": 58,
      "detectedColor": "Gold",
      "confidence": 98.2
    }
  ]
}
```

---

## 📸 Best Practices
To ensure **>95% accuracy**, follow the "Golden Screenshot" rules:
1. **Full Screen:** Send original screenshots. Do not crop the image manually.
2. **No Overlays:** Close the chat, notification bubbles, or side menus before capturing.
3. **Brightness:** Use standard in-game brightness for optimal contrast.

---

## Support the Project
If RoKVision helps your alliance, consider buying me a coffee! ☕
- Pix: 031c9e65-66a3-4611-822b-796e227e200a
- Ko-fi: [link]

---

## 🤝 Contributing
See our [CONTRIBUTING.md](CONTRIBUTING.md) for details on how to help the project.

Pull requests are welcome! For major changes, please open an issue first.

### 📝 License
Distributed under the MIT License. See `LICENSE` for more information.
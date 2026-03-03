# 🛡️ RoK Vision API - Reference

This document provides a quick overview of the available endpoints, request formats, and the extracted data payload (`data.summary`). For architectural details, refer to the [README.md](README.md).

## 📦 The Standard Envelope

All successful API responses return a standardized JSON envelope. To keep this documentation clean, the endpoint examples below will only show the **`data.summary`** object, which contains the final, clean data you will actually use.

```jsonc
{
  "meta": { "apiVersion": "1.0.0", "requestId": "...", "timestamp": "..." },
  "status": { "success": true, "code": 200, "overallConfidence": 95.5, "warnings": [] },
  "data": {
    "summary": { 
      // 👇 ALL EXAMPLES BELOW FOCUS ON THIS OBJECT 👇
    },
    "fields": { /* Detailed OCR evidence, bounding boxes, and confidence per field */ }
  },
  "auditLog": [ /* Step-by-step internal logic execution */ ],
  "debug": { /* Execution timings and raw text (Requires 'Debug: true' in request) */ }
}
```

---

## 🚀 Endpoints

### `POST /api/governor/analyze`
Extracts core stats from a Governor Profile screenshot.

*   **Request Body (`multipart/form-data`)**
    *   `image` (file): Governor profile screenshot.
    *   `Debug` (boolean, optional): Set to `true` to receive raw OCR text and timings.

*   **Response (`data.summary`)**
    ```json
    {
      "id": 197230311,
      "name": "Feels",
      "allianceTag": "SAZZ",
      "allianceName": "Silent Ascent",
      "power": 7336199,
      "killPoints": 963394,
      "civilization": "China",
      "isSuccessfulRead": true
    }
    ```

---

### `POST /api/reports/analyze`
Analyzes a battle report (PvP or PvE), extracting commanders, casualties, and NPC stats.

*   **Request Body (`multipart/form-data`)**
    *   `image` (file): Battle report screenshot.
    *   `Debug` (boolean, optional)

*   **Response (`data.summary`)**
    ```jsonc
    {
      "type": 1, // 1 = PvP, 3 = PvE
      "attacker": {
        "governorName": "Feels",
        "allianceTag": "--",
        "isNpc": false,
        "primaryCommander": { "id": "boudica_old", "canonicalName": "Boudica", "rarity": "Epic" },
        "secondaryCommander": { "id": "pericles", "canonicalName": "Pericles", "rarity": "Epic" },
        "totalUnits": 40342,
        "severelyWounded": 19287,
        "dead": 0,
        // ... healed, slightlyWounded, remaining, killPointsGained
        "casualtyRate": 0.478
      },
      "defender": {
        "governorName": "Kranitos",
        "isNpc": true, // Example of a Barbarian/Fort PvE target
        "pveStats": { "damageReceivedPercentage": 20.3, "entityType": "Barbarian" }
        // ... commander and troops data
      },
      "timestamp": "2026-12-19T00:00:00",
      "isVictoryForAttacker": false
    }
    ```

---

### `POST /api/ap/analyze`
Reads Action Points (AP) from the inventory. **Supports multiple images** (scrolling) and automatically resolves item overlap conflicts.

*   **Request Body (`multipart/form-data`)**
    *   `images[]` (file array): One or multiple AP inventory screenshots.
    *   `Debug` (boolean, optional)

*   **Response (`data.summary`)**
    ```jsonc
    {
      "grandTotalAp": 338750,
      "currentBarValue": 875,
      "maxBarValue": 1000,
      "items": [
        {
          "itemId": "AP_100",
          "name": "Basic Action Point Recovery",
          "unitValue": 100,
          "quantity": 2086,
          "totalValue": 208600,
          "confidence": 99.1
        }
        // ... other detected AP items (AP_50, AP_500, etc.)
      ]
    }
    ```

---

### `POST /api/xp/analyze`
Reads Tomes of Knowledge (XP) from the inventory. Includes color filtering to ignore non-XP items. Supports multiple images.

*   **Request Body (`multipart/form-data`)**
    *   `images[]` (file array): One or multiple XP inventory screenshots.
    *   `Debug` (boolean, optional)

*   **Response (`data.summary`)**
    ```jsonc
    {
      "totalXp": 984780300,
      "items": [
        {
          "itemId": "XP_50000",
          "unitValue": 50000,
          "quantity": 8940,
          "totalXp": 447000000,
          "confidence": 99.98,
          "detectedColor": "Blue"
        }
        // ... other detected XP tomes (XP_100, XP_1000, etc.)
      ]
    }
    ```

---

### `POST /api/map/analyze` (Beta)
Uses a Hybrid AI Engine (YOLOv8 + OCR) to extract visible cities from a Kingdom Map screenshot.

*   **Request Body (`multipart/form-data`)**
    *   `image` (file): Kingdom map screenshot.
    *   `Debug` (boolean, optional)

*   **Response (`data.summary`)**
    ```jsonc
    {
      "kingdomNumber": 3746,
      "x": 322,
      "y": 899,
      "cities": [
        {
          "name": "Feels",
          "allianceTag": "Ab46",
          "hasShield": true,
          "screenLocation": {
            "cx": 1115.5,
            "cy": 501
          }
        }
      ]
    }
    ```
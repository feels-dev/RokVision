from fastapi import APIRouter, HTTPException
from app.schemas.requests import BatchAnalyzeRequest
from app.services.image_processing import ImageProcessor
from app.core.engine import OcrEngine
import logging

router = APIRouter()
logger = logging.getLogger(__name__)

@router.post("/process")
async def process_batch(request: BatchAnalyzeRequest):
    # 1. Decode Full Image once
    full_img = ImageProcessor.base64_to_cv2(request.imageBase64)
    if full_img is None:
        raise HTTPException(status_code=400, detail="Invalid Image")

    ocr = OcrEngine.get_instance()
    results = []

    for region in request.regions:
        x, y, w, h = region.box
        
        # Crop & Filter
        processed_crop = ImageProcessor.process_region(
            full_img, int(x), int(y), int(w), int(h), region.strategy
        )

        if processed_crop is None:
            results.append({"id": region.id, "text": "", "conf": 0.0, "strategy": region.strategy})
            continue

        # --- CRITICAL CHANGE: det=True ---
        # We enable detection so it finds the "lost" number inside the crop
        ocr_res = ocr.ocr(processed_crop, cls=False, det=True)

        text = ""
        conf = 0.0
        
        # The format with det=True is: [[ [box], (text, conf) ], ... ]
        if ocr_res and ocr_res[0]:
            # We take the block with the highest confidence or concatenate if more than one.
            # For XP, it is usually a single number. We take the best candidate.
            best_line = max(ocr_res[0], key=lambda x: x[1][1])
            text = best_line[1][0]
            conf = float(best_line[1][1])

        results.append({
            "id": region.id,
            "text": text,
            "conf": conf,
            "strategy": region.strategy
        })

    return {"success": True, "results": results}
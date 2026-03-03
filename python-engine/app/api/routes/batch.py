# Path: app/api/routes/batch.py

import logging
from fastapi import APIRouter, HTTPException
from app.schemas.requests import BatchAnalyzeRequest
from app.services.image_processing import ImageProcessor
from app.core.engine import OcrEngine

router = APIRouter()
logger = logging.getLogger(__name__)

@router.post("/process")
async def process_batch(request: BatchAnalyzeRequest):
    """
    Processes multiple regions of a single Base64 image.
    Applies filter strategies (like 'map_label_enhanced') and executes OCR.
    """
    try:
        # 1. Decode Full Image once to save CPU
        full_img = ImageProcessor.base64_to_cv2(request.imageBase64)
        if full_img is None:
            raise HTTPException(status_code=400, detail="Invalid Base64 Image Data")

        # Get OCR Singleton
        ocr = OcrEngine.get_instance()
        results = []

        for region in request.regions:
            # Ensure integers for cropping
            x, y, w, h = map(int, region.box)
            
            # 2. Crop & Apply Filters (Strategy Pattern)
            # Strategy 'map_label_enhanced' must be handled within process_region
            processed_crop = ImageProcessor.process_region(
                full_img, x, y, w, h, region.strategy
            )

            # If crop fails (invalid dimensions, etc)
            if processed_crop is None:
                results.append({
                    "id": region.id,
                    "text": "",
                    "conf": 0.0,
                    "strategy": region.strategy
                })
                continue

            # 3. OCR Execution with Detection (det=True)
            # cls=False: Small crops usually don't need angle classification
            # det=True: Essential for finding "loose" text inside expanded box
            ocr_res = ocr.ocr(processed_crop, cls=False, det=True)

            text_output = ""
            conf_output = 0.0
            
            # PaddleOCR structure: [ [ [[x,y],..], (text, conf) ], ... ]
            if ocr_res and ocr_res[0]:
                # If multiple lines exist (e.g., Tag above, Name below),
                # sort by Y (height) and join them.
                lines = sorted(ocr_res[0], key=lambda r: r[0][0][1]) # Sort by Y
                
                texts = [line[1][0] for line in lines]
                confs = [line[1][1] for line in lines]
                
                text_output = " ".join(texts)
                # Average confidence
                conf_output = sum(confs) / len(confs) if confs else 0.0

            results.append({
                "id": region.id,
                "text": text_output,
                "conf": float(conf_output),
                "strategy": region.strategy
            })

        return {"success": True, "results": results}

    except Exception as e:
        logger.error(f"Batch Processing Error: {str(e)}", exc_info=True)
        return {"success": False, "results": [], "error": str(e)}
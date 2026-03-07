# python-engine/app/api/routes/map.py

from fastapi import APIRouter, UploadFile, File, HTTPException
from app.services.yolo_detector import YoloDetector
from app.services.storage import LocalImageStorage
import os

router = APIRouter()
storage = LocalImageStorage()

# Load model
MODEL_PATH = os.getenv("MODEL_PATH", "app/models/rok_map_detector_v1.onnx")
# ADJUSTMENT 1: Lower confidence_thresh to 0.10 (RoK is visually cluttered)
detector = YoloDetector(model_path=MODEL_PATH, confidence_thresh=0.10)

@router.post("/detect", summary="Detects objects on the RoK map")
async def detect_map_objects(image: UploadFile = File(...)):
    if not image.content_type.startswith("image/"):
        raise HTTPException(status_code=400, detail="The file you sent is not an image..")

    file_path = ""
    try:
        file_path = await storage.save_temp_image(image)
        detections = detector.detect_objects(file_path)

        # ADJUSTMENT 2: Return exact structure expected by C# (Success + Detections)
        return {
            "success": True, 
            "detections": detections,
            "count": len(detections)
        }

    except Exception as e:
        print(f"[MapRoute] Error in Python Map Detect: {str(e)}")
        return {"success": False, "detections": [], "error": str(e)}
        
    finally:
        if file_path and os.path.exists(file_path):
            storage.delete_temp_image(file_path)
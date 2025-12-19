import base64
import numpy as np
import cv2
import uvicorn
import os
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from paddleocr import PaddleOCR

app = FastAPI(title="RoK Vision API")

# --- HARDWARE CONFIGURATION ---
USE_GPU = os.getenv('OCR_USE_GPU', 'False').lower() == 'true'
ENABLE_MKLDNN = os.getenv('OCR_ENABLE_MKLDNN', 'True').lower() == 'true'
CPU_THREADS = int(os.getenv('OCR_CPU_THREADS', '4'))

print(f"🚀 STARTING OCR ENGINE | GPU: {USE_GPU} | MKLDNN: {ENABLE_MKLDNN}")

# Initialize PaddleOCR
ocr = PaddleOCR(
    use_angle_cls=False,
    lang='en',
    use_gpu=USE_GPU,
    enable_mkldnn=ENABLE_MKLDNN,
    cpu_threads=CPU_THREADS,
    show_log=False,
    ocr_version='PP-OCRv4' # Ensures usage of v4, which is faster and more accurate
)

# We now accept 'imageBase64' instead of 'filePath'
class OcrRequest(BaseModel):
    imageBase64: str

def base64_to_image(b64_str):
    try:
        # Decode the Base64 string into bytes
        img_bytes = base64.b64decode(b64_str)
        # Convert bytes to a Numpy array (format understood by OpenCV)
        nparr = np.frombuffer(img_bytes, np.uint8)
        # Decode into a color image
        img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        return img
    except Exception as e:
        print(f"Error converting image: {e}")
        return None

@app.post("/process")
def process_image(request: OcrRequest):
    try:
        # 1. Convert Base64 -> Image in RAM
        img = base64_to_image(request.imageBase64)
        
        if img is None:
            raise HTTPException(status_code=400, detail="Invalid or corrupted image (Base64 error).")

        # 2. OCR runs directly on the loaded image (no disk I/O)
        result = ocr.ocr(img, cls=False)
        
        blocks = []
        full_text_lines = []

        if result and result[0]:
            for line in result[0]:
                coords = line[0]
                text = line[1][0]
                confidence = line[1][1]
                
                if confidence > 0.5: 
                    full_text_lines.append(text)
                    blocks.append({
                        "text": text,
                        "box": coords,
                        "conf": confidence
                    })

        return {
            "success": True,
            "full_text": "\n".join(full_text_lines),
            "blocks": blocks
        }

    except Exception as e:
        print(f"🔥 Processing error: {str(e)}")
        raise HTTPException(status_code=500, detail=str(e))

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)
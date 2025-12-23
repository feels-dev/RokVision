from fastapi import APIRouter, HTTPException
from app.schemas.requests import OcrRequest
from app.services.image_processing import ImageProcessor
from app.core.engine import OcrEngine

router = APIRouter()

@router.post("/analyze")
async def analyze_governor(request: OcrRequest):
    img = ImageProcessor.base64_to_cv2(request.imageBase64)
    if img is None:
        raise HTTPException(status_code=400, detail="Invalid Image")

    # Resize otimiza muito o tempo de inferência do Paddle
    img_resized, ratio = ImageProcessor.resize_if_needed(img, max_width=1280)

    ocr = OcrEngine.get_instance()
    result = ocr.ocr(img_resized, cls=False)
    
    blocks = []
    full_text = []

    if result and result[0]:
        for line in result[0]:
            # Recupera as coordenadas originais multiplicando pelo ratio
            # Isso é opcional, mas bom se o C# desenhar caixas na imagem original
            box = line[0]
            if ratio != 1.0:
                 box = [[pt[0]/ratio, pt[1]/ratio] for pt in box]

            blocks.append({
                "text": line[1][0],
                "box": box,
                "conf": line[1][1]
            })
            full_text.append(line[1][0])

    return {
        "success": True,
        "full_text": "\n".join(full_text),
        "blocks": blocks
    }
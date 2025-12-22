import base64
import numpy as np
import cv2
import logging

# Setup logger for this module
logger = logging.getLogger(__name__)

def base64_to_cv2(b64_str: str):
    """Converts Base64 string to OpenCV format (BGR)."""
    try:
        if "," in b64_str:
            b64_str = b64_str.split(",")[1]
            
        img_bytes = base64.b64decode(b64_str)
        nparr = np.frombuffer(img_bytes, np.uint8)
        img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        return img
    except Exception as e:
        logger.error(f"❌ Error decoding base64: {e}")
        return None

def get_grayscale(image):
    return cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
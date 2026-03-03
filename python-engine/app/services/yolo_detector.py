# python-engine/app/services/yolo_detector.py

import cv2
import numpy as np
import onnxruntime as ort
from typing import List, Dict, Tuple

class YoloDetector:
    def __init__(self, model_path: str, confidence_thresh: float = 0.25, iou_thresh: float = 0.45):
        self.confidence_thresh = confidence_thresh
        self.iou_thresh = iou_thresh
        
        self.session = ort.InferenceSession(model_path, providers=['CPUExecutionProvider'])
        
        # Input Info
        model_inputs = self.session.get_inputs()
        self.input_name = model_inputs[0].name
        shape = model_inputs[0].shape
        
        # Define Input Size (Attempt to retrieve from model, fallback to 640)
        try:
            self.input_height = int(shape[2]) if isinstance(shape[2], (int, float)) else 640
            self.input_width = int(shape[3]) if isinstance(shape[3], (int, float)) else 640
        except:
            self.input_height = 640
            self.input_width = 640

        # Output Info
        model_outputs = self.session.get_outputs()
        self.output_name = model_outputs[0].name
        
        self.classes = ['city_label', 'shield']

    def detect_objects(self, image_path: str) -> List[Dict]:
        image = cv2.imread(image_path)
        if image is None:
            print(f"[YoloDetector] ERRO: Não foi possível ler a imagem {image_path}")
            return []
        
        input_tensor, ratio, (dw, dh) = self._preprocess(image)

        outputs = self.session.run([self.output_name], {self.input_name: input_tensor})
        
        # 3. Intelligent Post-processing (v5/v8 and Scale Back)
        detections = self._postprocess(outputs[0], ratio, (dw, dh))
        
        print(f"[YoloDetector] Imagem processada. Detecções finais: {len(detections)}")
        return detections

    def _preprocess(self, image: np.ndarray) -> Tuple[np.ndarray, float, Tuple[float, float]]:
        shape = image.shape[:2]  # Original [h, w]
        new_shape = (self.input_width, self.input_height)

        r = min(new_shape[0] / shape[0], new_shape[1] / shape[1])
        

        new_unpad = int(round(shape[1] * r)), int(round(shape[0] * r))
        

        dw, dh = new_shape[1] - new_unpad[0], new_shape[0] - new_unpad[1]
        dw /= 2  
        dh /= 2


        if shape[::-1] != new_unpad:
            image = cv2.resize(image, new_unpad, interpolation=cv2.INTER_LINEAR)
            
        top, bottom = int(round(dh - 0.1)), int(round(dh + 0.1))
        left, right = int(round(dw - 0.1)), int(round(dw + 0.1))
        

        image = cv2.copyMakeBorder(image, top, bottom, left, right, cv2.BORDER_CONSTANT, value=(114, 114, 114))


        image = image.transpose((2, 0, 1))
        image = np.expand_dims(image, 0)
        image = np.ascontiguousarray(image).astype(np.float32)
        image /= 255.0

        return image, r, (dw, dh)

    def _postprocess(self, output: np.ndarray, ratio: float, pad: Tuple[float, float]) -> List[Dict]:
        """
        Handles YOLOv5 (cx,cy,w,h,conf,cls...) and YOLOv8 (cx,cy,w,h,cls...) output formats.
        """
        output = np.squeeze(output)
        

        if output.shape[0] < output.shape[1]: 
           
            output = output.T

        boxes, scores, class_ids = [], [], []
        dw, dh = pad 

        # Architecture Detection (v5 vs v8)
        # v5: 5 (box+obj) + num_classes
        # v8: 4 (box) + num_classes
        cols = output.shape[1]
        is_yolov8 = cols == (4 + len(self.classes)) 

        # Debug Stats
        max_conf_found = 0.0

        for row in output:
            if is_yolov8:
                # YOLOv8: [x, y, w, h, class1_conf, class2_conf, ...]
                classes_scores = row[4:]
                class_id = np.argmax(classes_scores)
                confidence = classes_scores[class_id]
            else:
                # YOLOv5: [x, y, w, h, obj_conf, class1_conf, ...]
                obj_conf = row[4]
                if obj_conf < self.confidence_thresh: continue # Fast optimization
                
                classes_scores = row[5:]
                class_id = np.argmax(classes_scores)
                confidence = obj_conf * classes_scores[class_id]

            if confidence > max_conf_found: max_conf_found = confidence

            if confidence >= self.confidence_thresh:
                cx, cy, w, h = row[0], row[1], row[2], row[3]

                # 1. Remove Letterbox Padding
                cx = (cx - dw) / ratio
                cy = (cy - dh) / ratio
                w = w / ratio
                h = h / ratio
                
                # 2. Store [top, left, w, h] for NMS
                x = cx - w / 2
                y = cy - h / 2
                
                boxes.append([x, y, w, h])
                scores.append(float(confidence))
                class_ids.append(int(class_id))
        
        # Debug log to check if threshold is too high
        if not boxes:
            print(f"[YoloDetector] 0 Candidates. Max Confidence seen: {max_conf_found:.4f}")
            return []

        # Apply NMS
        indices = cv2.dnn.NMSBoxes(boxes, scores, self.confidence_thresh, self.iou_thresh)

        detections = []
        if len(indices) > 0:
            for i in indices.flatten():
                box = boxes[i]
                final_x, final_y, final_w, final_h = box
                
                # Protection against negative coordinates
                detections.append({
                    "class": self.classes[class_ids[i]],
                    "confidence": float(scores[i]),
                    "box": [
                        int(max(0, final_x)), 
                        int(max(0, final_y)), 
                        int(final_w), 
                        int(final_h)
                    ]
                })
        
        return detections
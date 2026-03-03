# /app/services/storage.py
import os
import uuid
import aiofiles
from fastapi import UploadFile

class LocalImageStorage:
    """
    Service to manage saving and deleting temporary images 
    on the local filesystem.
    """
    def __init__(self, temp_dir: str = "/app/wwwroot/uploads"):
        self.temp_dir = temp_dir
        # Ensure upload directory exists
        os.makedirs(self.temp_dir, exist_ok=True)

    async def save_temp_image(self, file: UploadFile) -> str:
        """
        Saves an UploadFile to a temporary location with a unique name.
        
        Args:
            file (UploadFile): The file sent via FastAPI.

        Returns:
            str: The full path to the saved file.
        """
        try:
            # Get original file extension (e.g., .jpg, .png)
            _, extension = os.path.splitext(file.filename)
            # Generate unique filename to prevent collisions
            unique_filename = f"{uuid.uuid4()}{extension}"
            file_path = os.path.join(self.temp_dir, unique_filename)

            # Save file asynchronously (performance optimization)
            async with aiofiles.open(file_path, 'wb') as out_file:
                content = await file.read()
                await out_file.write(content)
            
            return file_path
        except Exception as e:
            # Raise exception for the route to handle in case of error
            raise IOError(f"Falha ao salvar a imagem temporária: {e}")

    def delete_temp_image(self, file_path: str):
        """
        Deletes a file from the filesystem.
        
        Args:
            file_path (str): The full path to the file to be deleted.
        """
        if file_path and os.path.exists(file_path):
            try:
                os.remove(file_path)
            except OSError as e:
                # Log error but do not crash application if deletion fails
                print(f"Erro ao deletar arquivo temporário {file_path}: {e}")
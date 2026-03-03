# eKYC Inference API

Optional Flask microservice providing Thai ID card text extraction using Tesseract OCR. Deployed as a Docker container on ECS/Fargate when provisioned via the CDK infrastructure stack.

**Note:** This service is optional. The main eKYC API functions without it. It adds Thai language OCR capabilities.

## Technology Stack

- Python 3
- Flask
- OpenCV (cv2)
- Tesseract OCR
- Pillow

## API Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/` | Health check |
| `GET` | `/healthcheck` | Health check |
| `POST` | `/thai` | Extract Thai text from an uploaded image file |
| `POST` | `/thai/id/front` | Extract structured field data from Thai ID card front |
| `POST` | `/thai/id/back` | Extract structured field data from Thai ID card back |

## Configuration

- Runs on port 8000
- Max upload size: 10 MB

## Local Development

Install dependencies:

```bash
pip install -r requirements.txt
```

Run the service:

```bash
python app.py
```

## Docker

Build the image:

```bash
docker build -t ekyc-inference-api .
```

Run the container:

```bash
docker run -p 8000:8000 ekyc-inference-api
```

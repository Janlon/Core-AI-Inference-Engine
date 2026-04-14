from __future__ import annotations

import logging
from datetime import UTC, datetime

from fastapi import FastAPI
from fastapi.responses import HTMLResponse, RedirectResponse

from app.agents.heuristic_agent import HeuristicNlpAgent
from app.agents.spacy_agent import SpacyNlpAgent
from app.models import HealthResponse, InferenceRequest, NlpProcessResponse
from app.services.inference_service import InferenceService
from app.settings import settings

logging.basicConfig(level=getattr(logging, settings.log_level.upper(), logging.INFO))

agents = []
if settings.spacy_enabled:
    agents.append(SpacyNlpAgent(settings.spacy_model))
if settings.heuristic_enabled:
    agents.append(HeuristicNlpAgent())

service = InferenceService(agents)

app = FastAPI(
    title="AIPort NLP Agent Service",
    version="0.1.0",
    description="Servico Python de NLP/agentes para integracao com o motor de inteligencia AIPort.",
        docs_url=None,
        redoc_url=None,
        openapi_url="/openapi/v1.json",
)


@app.get("/scalar/v1", include_in_schema=False, response_class=HTMLResponse)
@app.get("/v1/scalar", include_in_schema=False, response_class=HTMLResponse)
def scalar_docs() -> HTMLResponse:
        html = """
        <!doctype html>
        <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>AIPort NLP Agent Service - Scalar</title>
            </head>
            <body>
                <script
                    id="api-reference"
                    data-url="/openapi/v1.json"
                    data-theme="deepSpace"
                ></script>
                <script src="https://cdn.jsdelivr.net/npm/@scalar/api-reference"></script>
            </body>
        </html>
        """
        return HTMLResponse(content=html)


@app.get("/docs", include_in_schema=False)
def docs_redirect() -> RedirectResponse:
    return RedirectResponse(url="/v1/scalar", status_code=307)


@app.post("/api/inference/process", response_model=NlpProcessResponse)
def process_inference(request: InferenceRequest) -> NlpProcessResponse:
    return service.process(request)


@app.get("/api/inference/health", response_model=HealthResponse)
def health() -> HealthResponse:
    return HealthResponse(
        status="healthy",
        service=settings.service_name,
        utc=datetime.now(UTC).isoformat(),
    )
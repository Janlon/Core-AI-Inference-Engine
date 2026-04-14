from fastapi.testclient import TestClient

from app.main import app


client = TestClient(app)


def test_health_endpoint_returns_status() -> None:
    response = client.get("/api/inference/health")

    assert response.status_code == 200
    payload = response.json()
    assert payload["status"] == "healthy"
    assert payload["service"]


def test_docs_redirects_to_scalar() -> None:
    response = client.get("/docs", follow_redirects=False)

    assert response.status_code == 307
    assert response.headers["location"] == "/v1/scalar"


def test_openapi_is_exposed_under_v1_path() -> None:
    response = client.get("/openapi/v1.json")

    assert response.status_code == 200
    payload = response.json()
    assert payload["info"]["title"] == "AIPort NLP Agent Service"


def test_scalar_page_is_available() -> None:
    response = client.get("/v1/scalar")

    assert response.status_code == 200
    assert "@scalar/api-reference" in response.text
    assert "/openapi/v1.json" in response.text


def test_legacy_scalar_path_remains_available() -> None:
    response = client.get("/scalar/v1")

    assert response.status_code == 200
    assert "@scalar/api-reference" in response.text


def test_process_extracts_visitor_name_and_unit() -> None:
    response = client.post(
        "/api/inference/process",
        json={
            "Texto": "meu nome e Carlos, vou para o apartamento 204",
            "TenantType": "residential",
            "SessionId": "sessao-1",
            "Metadata": {"origem": "teste"},
        },
    )

    assert response.status_code == 200
    payload = response.json()
    assert payload["Intencao"] == "Identificacao"
    assert payload["DadosExtraidos"]["Nome"] == "Carlos"
    assert payload["DadosExtraidos"]["NomeVisitante"] == "Carlos"
    assert payload["DadosExtraidos"]["Unidade"] == "204"
    assert payload["Confianca"] > 0.0


def test_process_greeting_only_does_not_extract_name() -> None:
    response = client.post(
        "/api/inference/process",
        json={
            "Texto": "ola",
            "TenantType": "residential",
        },
    )

    assert response.status_code == 200
    payload = response.json()
    assert payload["Intencao"] == "Saudacao"
    assert payload["DadosExtraidos"]["Nome"] is None
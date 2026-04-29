#!/usr/bin/env python3
"""Lista modelos Gemini disponíveis para a GEMINI_API_KEY configurada."""

import json
import sys
import urllib.error
import urllib.request
from typing import Any, cast


API_URL = "https://generativelanguage.googleapis.com/v1beta/models"
INLINE_GEMINI_API_KEY = "AIzaSyBWNXhL-tZtAhO7-fJNPamAeFSRGX1MobA"
PLACEHOLDER_KEYS = {"your_gemini_api_key_here", "placeholder", "", "COLE_SUA_CHAVE_AQUI"}


def get_api_key() -> str | None:
    """Retorna a chave Gemini válida definida diretamente neste arquivo."""
    api_key = INLINE_GEMINI_API_KEY.strip()
    if api_key in PLACEHOLDER_KEYS:
        return None
    return api_key or None


def fetch_models(api_key: str) -> list[dict[str, Any]]:
    """Busca a lista de modelos exposta pela API Gemini."""
    request = urllib.request.Request(f"{API_URL}?key={api_key}", method="GET")
    with urllib.request.urlopen(request, timeout=20) as response:
        payload = json.loads(response.read().decode("utf-8"))
    return payload.get("models", [])


def stringify_methods(raw_methods: Any) -> list[str]:
    """Converte supportedGenerationMethods em uma lista segura de strings."""
    if not isinstance(raw_methods, list):
        return []
    methods: list[str] = []
    for item in cast(list[Any], raw_methods):
        methods.append(str(item))
    return methods


def print_models(models: list[dict[str, Any]]) -> None:
    """Exibe os modelos de forma resumida e filtrada para uso com generateContent."""
    if not models:
        print("Nenhum modelo retornado pela API.")
        return

    generate_models: list[dict[str, Any]] = []
    other_models: list[dict[str, Any]] = []

    for model in models:
        methods = stringify_methods(model.get("supportedGenerationMethods", []))
        if "generateContent" in methods:
            generate_models.append(model)
        else:
            other_models.append(model)

    print(f"Total de modelos retornados: {len(models)}")
    print(f"Modelos com generateContent: {len(generate_models)}")

    if generate_models:
        print("\nModelos Gemini utilizáveis neste projeto:")
        for model in generate_models:
            name = str(model.get("name", "desconhecido"))
            display_name = str(model.get("displayName", "sem displayName"))
            version = str(model.get("version", "sem versao"))
            token_limit = str(model.get("inputTokenLimit", "?"))
            print(f"- {name} | {display_name} | versao={version} | inputTokenLimit={token_limit}")

    if other_models:
        print("\nOutros modelos retornados pela API:")
        for model in other_models:
            name = str(model.get("name", "desconhecido"))
            methods_list = stringify_methods(model.get("supportedGenerationMethods", []))
            methods = ", ".join(methods_list) or "nenhum"
            print(f"- {name} | methods={methods}")


def main() -> int:
    api_key = get_api_key()
    if not api_key:
        print("A chave Gemini não foi definida em INLINE_GEMINI_API_KEY.")
        print("Edite este arquivo e cole a chave diretamente na constante no topo.")
        return 1

    try:
        models = fetch_models(api_key)
    except urllib.error.HTTPError as exc:
        error_body = exc.read().decode("utf-8", errors="replace")
        print(f"Erro HTTP ao consultar modelos Gemini: {exc.code}")
        print(error_body)
        return 2
    except urllib.error.URLError as exc:
        print(f"Erro de rede ao consultar modelos Gemini: {exc.reason}")
        return 3
    except Exception as exc:
        print(f"Erro inesperado ao consultar modelos Gemini: {exc}")
        return 4

    print_models(models)
    return 0


if __name__ == "__main__":
    sys.exit(main())
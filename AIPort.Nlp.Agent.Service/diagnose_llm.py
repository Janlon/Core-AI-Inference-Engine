#!/usr/bin/env python3
"""
Diagnóstico específico para problemas de LLM/API.
"""

import os
import sys
import asyncio
import json
from pathlib import Path

async def diagnose_llm_connection():
    """Diagnostica problemas de conectividade e configuração dos LLMs."""
    print("🔍 DIAGNÓSTICO DE CONECTIVIDADE LLM")
    print("=" * 50)
    
    # 1. Verificação de variáveis de ambiente
    print("📋 VERIFICAÇÃO DE CHAVES API:")
    
    gemini_key = os.getenv("AIPORT_AI_LLM_PRIMARY_API_KEY") or os.getenv("GEMINI_API_KEY")
    openai_key = os.getenv("OPENAI_API_KEY") 
    anthropic_key = os.getenv("ANTHROPIC_API_KEY")
    
    if gemini_key:
        if gemini_key in ["your_gemini_api_key_here", "placeholder"]:
            print("❌ GEMINI_API_KEY: Placeholder detectado - chave não real")
        elif len(gemini_key) < 10:
            print("❌ GEMINI_API_KEY: Muito curta - provavelmente inválida")
        else:
            print(f"✅ GEMINI_API_KEY: Configurada (primeiros chars: {gemini_key[:10]}...)")
    else:
        print("❌ GEMINI_API_KEY: NÃO encontrada no ambiente")
    
    if openai_key:
        if openai_key in ["your_openai_api_key_here", "placeholder"]:
            print("❌ OPENAI_API_KEY: Placeholder detectado")
        elif len(openai_key) < 10:
            print("❌ OPENAI_API_KEY: Muito curta")
        else:
            print(f"✅ OPENAI_API_KEY: Configurada (primeiros chars: {openai_key[:10]}...)")
    else:
        print("❌ OPENAI_API_KEY: NÃO encontrada")
    
    if anthropic_key:
        if anthropic_key in ["your_anthropic_api_key_here", "placeholder"]:
            print("❌ ANTHROPIC_API_KEY: Placeholder detectado")
        elif len(anthropic_key) < 10:
            print("❌ ANTHROPIC_API_KEY: Muito curta")
        else:
            print(f"✅ ANTHROPIC_API_KEY: Configurada (primeiros chars: {anthropic_key[:10]}...)")
    else:
        print("❌ ANTHROPIC_API_KEY: NÃO encontrada")
    
    # 2. Verifica conectividade básica
    print("\n🌐 VERIFICAÇÃO DE CONECTIVIDADE:")
    
    try:
        import urllib.request
        urllib.request.urlopen('https://www.google.com', timeout=5)
        print("✅ Conectividade internet: OK")
    except Exception as e:
        print(f"❌ Conectividade internet: FALHA - {e}")
        return False
    
    # 3. Teste específico de APIs
    print("\n🧪 TESTE DIRETO DE APIs:")
    
    # Teste Gemini se disponível
    if gemini_key and gemini_key not in ["your_gemini_api_key_here", "placeholder"]:
        try:
            import urllib.request
            import urllib.parse
            
            # Teste básico da API Gemini
            url = f"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent"
            
            data = {
                "contents": [{
                    "parts": [{"text": "Hello, this is a test"}]
                }]
            }
            
            headers = {
                'Content-Type': 'application/json',
            }
            
            full_url = f"{url}?key={gemini_key}"
            req = urllib.request.Request(full_url, 
                                       data=json.dumps(data).encode('utf-8'),
                                       headers=headers,
                                       method='POST')
            
            try:
                response = urllib.request.urlopen(req, timeout=10)
                if response.status == 200:
                    print("✅ API Gemini: Conectiva e funcionando")
                else:
                    print(f"⚠️ API Gemini: Status HTTP {response.status}")
            except Exception as api_error:
                error_msg = str(api_error)
                if "403" in error_msg or "401" in error_msg:
                    print("❌ API Gemini: Chave inválida ou sem permissão")
                elif "429" in error_msg:
                    print("⚠️ API Gemini: Limite de uso excedido")
                elif "timeout" in error_msg.lower():
                    print("⚠️ API Gemini: Timeout na conexão")
                else:
                    print(f"❌ API Gemini: Erro - {error_msg}")
                    
        except ImportError:
            print("⚠️ Módulos para teste de API não disponíveis")
        except Exception as e:
            print(f"❌ Erro no teste da API Gemini: {e}")
    
    # 4. Verificação de configuração
    print("\n⚙️ VERIFICAÇÃO DE CONFIGURAÇÃO:")
    
    config_paths = ["config/config.json", "config.json"]
    config_found = False
    
    for config_path in config_paths:
        if os.path.exists(config_path):
            try:
                with open(config_path, 'r', encoding='utf-8') as f:
                    config = json.load(f)
                
                llm_config = config.get("llm_providers", {})
                if llm_config.get("enabled", False):
                    print(f"✅ Configuração LLM: Habilitada em {config_path}")
                    
                    order = llm_config.get("order", [])
                    print(f"📋 Ordem de provedores: {order}")
                    
                    providers = llm_config.get("providers", {})
                    for provider_name, provider_config in providers.items():
                        enabled = provider_config.get("enabled", False)
                        status = "✅ Habilitado" if enabled else "❌ Desabilitado"
                        print(f"   {provider_name}: {status}")
                        
                    config_found = True
                    break
                else:
                    print(f"❌ Configuração LLM: Desabilitada em {config_path}")
                    
            except Exception as e:
                print(f"❌ Erro ao ler {config_path}: {e}")
    
    if not config_found:
        print("❌ Arquivo de configuração não encontrado")
    
    # 5. Resumo e recomendações
    print("\n🎯 RESUMO E RECOMENDAÇÕES:")
    
    if not gemini_key or gemini_key in ["your_gemini_api_key_here", "placeholder"]:
        print("🔧 AÇÃO NECESSÁRIA: Configure a GEMINI_API_KEY no arquivo .env")
        print("   1. Acesse: https://aistudio.google.com")
        print("   2. Gere uma API Key")
        print("   3. Cole no .env: GEMINI_API_KEY=sua_chave_aqui")
    
    return True

if __name__ == "__main__":
    asyncio.run(diagnose_llm_connection())
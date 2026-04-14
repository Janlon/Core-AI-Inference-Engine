from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_prefix="AIPORT_PY_NLP_", extra="ignore")

    service_name: str = "AIPort.Nlp.Agent.Service"
    log_level: str = "INFO"
    spacy_enabled: bool = True
    spacy_model: str = "pt_core_news_sm"
    heuristic_enabled: bool = True


settings = Settings()
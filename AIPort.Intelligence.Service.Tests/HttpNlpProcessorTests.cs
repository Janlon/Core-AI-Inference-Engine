using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AIPort.Intelligence.Service.Domain.Enums;
using AIPort.Intelligence.Service.Domain.Models;
using AIPort.Intelligence.Service.Domain.Options;
using AIPort.Intelligence.Service.Services.Engines;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AIPort.Intelligence.Service.Tests;

public sealed class HttpNlpProcessorTests
{
    [Fact]
    public async Task ProcessAsync_MapsExternalResponseAndPreservesRequestSignature()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();

            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("http://localhost:8010/api/inference/process", request.RequestUri!.ToString());

            var json = """
            {
              "Intencao": "Identificacao",
              "DadosExtraidos": {
                "Nome": "Carlos",
                "NomeVisitante": "Carlos",
                "Documento": null,
                "Cpf": null,
                "Unidade": "204",
                "Bloco": null,
                "Torre": null,
                "Empresa": null,
                "Parentesco": null,
                "EstaComVeiculo": false,
                "Placa": null,
                "EEntregador": false
              },
              "Confianca": 0.86,
              "Camada": "NLP-Agent-Orchestrator"
            }
            """;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:8010")
        };

        var sut = new HttpNlpProcessor(
            httpClient,
            Options.Create(new AIServiceOptions
            {
                Nlp = new NlpConfig
                {
                    Enabled = true,
                    UseExternalApi = true,
                    ExternalApiBaseUrl = "http://localhost:8010",
                    ExternalApiTimeoutMs = 3000
                }
            }),
            NullLogger<HttpNlpProcessor>.Instance);

        var state = new ProcessingState(
            "meu nome e Carlos, apartamento 204",
            "residential",
            "sessao-1",
            new Dictionary<string, string> { ["origem"] = "teste" });

        var result = await sut.ProcessAsync(state.Texto, state, CancellationToken.None);

        Assert.NotNull(capturedBody);
        Assert.Contains("\"Texto\":\"meu nome e Carlos, apartamento 204\"", capturedBody, StringComparison.Ordinal);
        Assert.Contains("\"TenantType\":\"residential\"", capturedBody, StringComparison.Ordinal);
        Assert.Contains("\"SessionId\":\"sessao-1\"", capturedBody, StringComparison.Ordinal);
        Assert.Contains("\"origem\":\"teste\"", capturedBody, StringComparison.Ordinal);

        Assert.Equal(Intencao.Identificacao, result.Intencao);
        Assert.Equal("Carlos", result.DadosExtraidos.Nome);
        Assert.Equal("Carlos", result.DadosExtraidos.NomeVisitante);
        Assert.Equal("204", result.DadosExtraidos.Unidade);
        Assert.Equal(0.86, result.Confianca, 3);
        Assert.Equal("NLP-Agent-Orchestrator", result.Camada);
    }

    [Fact]
    public async Task ProcessAsync_InvalidBaseUrl_ReturnsStateWithoutExternalCall()
    {
        var sut = new HttpNlpProcessor(
            new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("Nao deveria chamar HTTP"))),
            Options.Create(new AIServiceOptions
            {
                Nlp = new NlpConfig
                {
                    Enabled = true,
                    UseExternalApi = true,
                    ExternalApiBaseUrl = "url-invalida"
                }
            }),
            NullLogger<HttpNlpProcessor>.Instance);

        var state = new ProcessingState("ola", "residential")
        {
            Intencao = Intencao.Saudacao,
            MelhorConfianca = 0.2
        };

        var result = await sut.ProcessAsync(state.Texto, state, CancellationToken.None);

        Assert.Equal(Intencao.Saudacao, result.Intencao);
        Assert.Equal("NLP-ExternalApiInvalidConfig", result.Camada);
        Assert.Equal(0.0, result.Confianca, 3);
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => handler(request);
    }
}
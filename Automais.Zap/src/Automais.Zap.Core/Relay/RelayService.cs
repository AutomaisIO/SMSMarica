using Automais.Zap.Core.Entregas;
using Automais.Zap.Core.Meta;
using Automais.Zap.Core.Roteamento;
using Automais.Zap.Data;
using Automais.Zap.Data.Entities;
using Microsoft.Extensions.Logging;

namespace Automais.Zap.Core.Relay;

/// <summary>
/// O relay inteiro. Confere a assinatura, descobre de quem é cada evento, entrega, e conta.
/// Não interpreta mensagem e não guarda conteúdo.
/// </summary>
public sealed class RelayService(
    ZapDbContext db,
    IRoteador roteador,
    IEntregador entregador,
    IConfiguracaoMetaService configuracao,
    TimeProvider relogio,
    ILogger<RelayService> logger) : IRelayService
{
    public async Task<ResultadoRelay> ProcessarAsync(
        byte[] corpo, string? assinatura, CancellationToken ct = default)
    {
        var appSecret = (await configuracao.ObterAsync(ct)).AppSecret;

        // Falha FECHADO. O webhook do monolito tem um catch vazio que, sem configuração,
        // aceita qualquer POST sem conferir HMAC — não repetir esse erro aqui, onde o
        // payload é de vários municípios.
        if (string.IsNullOrWhiteSpace(appSecret))
        {
            logger.LogError("Meta:AppSecret não configurado — recusando o webhook.");
            return new ResultadoRelay(SituacaoRelay.NaoConfigurado, 0, 0, 0);
        }

        if (!AssinaturaMeta.Confere(appSecret, corpo, assinatura))
        {
            logger.LogWarning("Assinatura inválida no webhook da Meta.");
            return new ResultadoRelay(SituacaoRelay.AssinaturaInvalida, 0, 0, 0);
        }

        if (!PayloadMeta.TentarLer(corpo, out var payload, out var erroLeitura) || payload is null)
        {
            logger.LogWarning("Payload ilegível: {Erro}", erroLeitura);
            return new ResultadoRelay(SituacaoRelay.PayloadInvalido, 0, 0, 0);
        }

        using (payload)
        {
            var agora = relogio.GetUtcNow();

            var porNumero = await roteador.ResolverPorNumeroAsync(
                payload.Eventos.Where(e => e.PhoneNumberId is not null)
                    .Select(e => e.PhoneNumberId!).Distinct().ToList(), ct);

            var porWaba = await roteador.ResolverPorWabaAsync(
                payload.Eventos.Where(e => e.PhoneNumberId is null && e.WabaId is not null)
                    .Select(e => e.WabaId!).Distinct().ToList(), ct);

            var grupos = new Dictionary<Guid, GrupoDestino>();
            var semRota = new List<EventoMeta>();

            foreach (var ev in payload.Eventos)
            {
                RotaDestino? rota = null;
                if (ev.PhoneNumberId is not null)
                {
                    porNumero.TryGetValue(ev.PhoneNumberId, out rota);
                }
                else if (ev.WabaId is not null)
                {
                    porWaba.TryGetValue(ev.WabaId, out rota);
                }

                if (rota is null)
                {
                    semRota.Add(ev);
                    continue;
                }

                if (!grupos.TryGetValue(rota.TenantId, out var grupo))
                {
                    grupo = new GrupoDestino(rota);
                    grupos[rota.TenantId] = grupo;
                }

                grupo.Coordenadas.Add((ev.IndiceEntry, ev.IndiceChange));
                grupo.Eventos.Add(ev);
            }

            foreach (var ev in semRota)
            {
                logger.LogWarning(
                    "Evento sem rota: phone_number_id={Numero} waba={Waba} field={Field}. Cadastre o número na tela.",
                    ev.PhoneNumberId ?? "(ausente)", ev.WabaId ?? "(ausente)", ev.Field);

                db.EntregasLog.Add(new EntregaLog
                {
                    PhoneNumberId = ev.PhoneNumberId ?? ev.WabaId ?? "(ausente)",
                    TenantId = null,
                    Tipo = ev.Field,
                    Sucesso = false,
                    StatusHttp = null,
                    DuracaoMs = 0,
                    Erro = "sem rota cadastrada",
                    RecebidoEm = agora,
                });
            }

            var entregues = 0;
            var falhas = 0;

            foreach (var grupo in grupos.Values)
            {
                byte[] corpoDestino;
                string assinaturaDestino;

                if (grupo.Coordenadas.Count == payload.TotalEventos)
                {
                    // Caminho comum: o POST inteiro é deste destino. Encaminha byte a byte com a
                    // assinatura original — sem reserializar, sem chance de divergir do que a Meta
                    // assinou.
                    corpoDestino = corpo;
                    assinaturaDestino = assinatura!;
                }
                else
                {
                    // Lote com mais de um dono. Recorta e reassina com o MESMO App Secret: sob o
                    // App único, a instância valida igual e não fica sabendo que houve recorte.
                    // Sem isso, entregar o corpo inteiro faria o município A ver mensagem do B.
                    corpoDestino = payload.Fatiar(grupo.Coordenadas);
                    assinaturaDestino = AssinaturaMeta.Calcular(appSecret, corpoDestino);
                    logger.LogInformation(
                        "Payload com mais de um destino: recortando {N} de {Total} eventos para {Destino}.",
                        grupo.Coordenadas.Count, payload.TotalEventos, grupo.Rota.Nome);
                }

                var resultado = await entregador.EntregarAsync(
                    grupo.Rota.UrlWebhook, corpoDestino, assinaturaDestino, ct);

                if (resultado.Sucesso) entregues++;
                else falhas++;

                db.EntregasLog.Add(new EntregaLog
                {
                    PhoneNumberId = Truncar(string.Join(",", grupo.Eventos
                        .Select(e => e.PhoneNumberId ?? e.WabaId ?? "?").Distinct()), 200),
                    TenantId = grupo.Rota.TenantId,
                    Tipo = Truncar(string.Join(",", grupo.Eventos.Select(e => e.Field).Distinct()), 120),
                    Sucesso = resultado.Sucesso,
                    StatusHttp = resultado.StatusHttp,
                    DuracaoMs = resultado.DuracaoMs,
                    Erro = resultado.Erro,
                    RecebidoEm = agora,
                });
            }

            await db.SaveChangesAsync(ct);

            var situacao = falhas > 0 ? SituacaoRelay.FalhaDeEntrega : SituacaoRelay.Ok;
            return new ResultadoRelay(situacao, entregues, falhas, semRota.Count);
        }
    }

    private static string Truncar(string s, int max) => s.Length <= max ? s : s[..max];

    private sealed class GrupoDestino(RotaDestino rota)
    {
        public RotaDestino Rota { get; } = rota;
        public HashSet<(int Entry, int Change)> Coordenadas { get; } = [];
        public List<EventoMeta> Eventos { get; } = [];
    }
}

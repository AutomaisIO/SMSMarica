using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Core.RoboAtendimento.Comandos;
using SMSMais.Core.RoboAtendimento.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Runtime;

public interface IRoboSimulacaoService
{
    Task<RoboSimulacaoDto> SimularAsync(SimularRoboRequest request, CancellationToken ct = default);
}

/// <summary>
/// Ensaia um turno do robô <b>sem falar com o cidadão</b>: mesmo prompt, mesmas ferramentas, mesmo
/// modelo — só que nada é enviado ao WhatsApp e os comandos de ESCRITA não executam de verdade.
///
/// É o que torna o religamento seguro depois do incidente de 26/08: dá para ver a resposta, os
/// comandos que o robô chamaria, o custo e a latência antes de deixá-lo falar com alguém.
/// Sempre usa o motor da <b>Messages API</b>, mesmo com a configuração ainda em "Assinatura" — é
/// justamente o motor novo que se quer ensaiar.
/// </summary>
public sealed class RoboSimulacaoService(
    IHttpClientFactory httpClientFactory,
    SmsMaisDbContext db,
    IProtetorSegredos protetor,
    IRoboComandoDispatcher dispatcherReal,
    IRoboClassificador classificador,
    ILoggerFactory loggerFactory) : IRoboSimulacaoService
{
    public async Task<RoboSimulacaoDto> SimularAsync(SimularRoboRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Mensagem))
            throw new ValidacaoException("mensagem", "Escreva a mensagem do cidadão para simular.");

        var cfg = await db.RoboConfiguracoes.AsNoTracking().FirstOrDefaultAsync(ct);
        var personaGlobal = cfg?.PersonaGlobal ?? Data.Entities.Robo.RoboConfiguracao.PersonaGlobalPadrao;
        var modeloPadrao = cfg?.ModeloPadrao ?? "claude-haiku-4-5-20251001";

        var assuntoId = request.AssuntoId ?? await classificador.ClassificarAsync(request.Mensagem, ct);
        var assunto = assuntoId is { } id
            ? await db.RoboAssuntos.AsNoTracking()
                .Include(a => a.Treinos)
                .Include(a => a.Comandos)
                .FirstOrDefaultAsync(a => a.Id == id && a.ExcluidoEm == null, ct)
            : null;

        var dentroHorario = assunto is null || RoboPrompt.DentroDoHorario(assunto);
        // Mesma leitura do processador — a URL do app entra na despedida.
        var urlApp = await db.Instituicoes.AsNoTracking().Select(i => i.UrlApp).FirstOrDefaultAsync(ct);
        var comandos = assunto is null
            ? Array.Empty<string>()
            : [.. assunto.Comandos.Where(c => c.Habilitado).Select(c => c.Comando.ToString())];

        var entrada = new EntradaMotorRobo(
            ChaveSessao: $"simulacao:{Guid.CreateVersion7()}",
            // Conversa/paciente inexistentes de propósito: qualquer comando que dependa deles
            // devolve "não encontrado" em vez de tocar dado real.
            ConversaId: Guid.Empty,
            PacienteId: null,
            AssuntoId: assunto?.Id,
            Modelo: string.IsNullOrWhiteSpace(assunto?.Modelo) ? modeloPadrao : assunto!.Modelo!,
            InstrucaoSistema: RoboPrompt.MontarInstrucao(personaGlobal, assunto, dentroHorario, urlApp),
            ComandosHabilitados: comandos,
            Historico: [.. (request.Historico ?? []).Select(h => new MensagemHistoricoRobo(h.Papel, h.Texto))],
            MensagemAtual: request.Mensagem,
            DentroDoHorario: dentroHorario);

        var espiao = new DispatcherDeSimulacao(dispatcherReal);
        var motor = new RoboAtendimentoMotorApi(
            httpClientFactory.CreateClient(nameof(RoboAtendimentoMotorApi)),
            db, protetor, espiao, loggerFactory.CreateLogger<RoboAtendimentoMotorApi>());

        var cronometro = Stopwatch.StartNew();
        var resposta = await motor.ResponderAsync(entrada, ct);
        cronometro.Stop();

        return new RoboSimulacaoDto(
            assunto?.Nome,
            entrada.Modelo,
            resposta.Texto,
            resposta.HandOff,
            resposta.MotivoHandOff,
            resposta.Confianca,
            espiao.Chamadas,
            resposta.TokensEntrada,
            resposta.TokensSaida,
            resposta.CustoUsd,
            (long)cronometro.ElapsedMilliseconds,
            dentroHorario);
    }

    /// <summary>
    /// Deixa passar o que é LEITURA e finge o que é ESCRITA. Sem isso a simulação confirmaria
    /// presença de verdade, registraria pendência de cadastro, mandaria a conversa para a fila —
    /// exatamente os efeitos colaterais que a simulação existe para evitar. Registra tudo o que o
    /// modelo pediu, para a tela mostrar.
    /// </summary>
    private sealed class DispatcherDeSimulacao(IRoboComandoDispatcher real) : IRoboComandoDispatcher
    {
        private static readonly IReadOnlySet<ComandoRobo> Escrita =
            ComandoRoboCatalogo.Itens.Where(i => i.Escrita).Select(i => i.Comando).ToHashSet();

        public List<RoboSimulacaoChamadaDto> Chamadas { get; } = [];

        public async Task<RoboComandoResultado> ExecutarAsync(
            Guid conversaId, Guid? pacienteId, Guid? assuntoId, ComandoRobo comando, JsonElement args,
            CancellationToken ct)
        {
            var entradaJson = args.ValueKind == JsonValueKind.Undefined ? null : args.GetRawText();

            if (Escrita.Contains(comando))
            {
                const string aviso = "SIMULAÇÃO: comando de escrita não executado. Siga a conversa como se tivesse dado certo.";
                Chamadas.Add(new RoboSimulacaoChamadaDto(comando.ToString(), entradaJson, aviso, true, true));
                return new RoboComandoResultado(true, aviso);
            }

            var r = await real.ExecutarAsync(conversaId, pacienteId, assuntoId, comando, args, ct);
            Chamadas.Add(new RoboSimulacaoChamadaDto(comando.ToString(), entradaJson, r.Mensagem, r.Sucesso, false));
            return r;
        }
    }
}

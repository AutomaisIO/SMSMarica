using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMSMarica.Core.Atendimentos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.PesquisasSatisfacao.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.PesquisasSatisfacao;

/// <summary>
/// Pesquisa de satisfação por atendimento. Ver <see cref="PesquisaSatisfacao"/> para o desenho
/// do token e <see cref="IPesquisasSatisfacaoService.JanelaDias"/> para o prazo.
/// </summary>
public sealed class PesquisasSatisfacaoService(
    SmsMaricaDbContext db,
    IAtendimentosService atendimentos,
    IUsuarioAtualAccessor usuarioAtual,
    IConfiguration configuration) : IPesquisasSatisfacaoService
{
    /// <summary>
    /// Perguntas aceitas. Fechar a lista é o que impede uma tela adulterada de gravar campo
    /// inventado no meio da série — e o que denuncia, no deploy, que a tela mudou e o servidor não.
    /// </summary>
    private static readonly HashSet<string> PerguntasValidas =
    [
        "geral", "espera", "equipe", "informacoes", "confianca", "limpeza", "comentario",
    ];

    private const int TamanhoMaximoComentario = 2_000;

    public async Task<PesquisaPublicaDto> ObterPorTokenAsync(Guid token, CancellationToken ct = default)
    {
        var p = await db.PesquisasSatisfacao.AsNoTracking().FirstOrDefaultAsync(x => x.Id == token, ct)
            ?? throw new NaoEncontradoException("Pesquisa de satisfação", token);

        return new PesquisaPublicaDto(
            p.UnidadeNome,
            p.AtendimentoEm,
            p.ExpiraEm,
            Expirada: DateTime.UtcNow > p.ExpiraEm,
            JaRespondida: p.RespondidaEm is not null,
            p.InstrumentoVersao);
    }

    public async Task ResponderPorTokenAsync(
        Guid token, IReadOnlyDictionary<string, string> respostas, string? ip, CancellationToken ct = default)
    {
        var p = await db.PesquisasSatisfacao.FirstOrDefaultAsync(x => x.Id == token, ct)
            ?? throw new NaoEncontradoException("Pesquisa de satisfação", token);

        Gravar(p, respostas, ip);
        await db.SaveChangesAsync(ct);
    }

    public async Task ResponderPeloAppAsync(
        Guid pacienteId, Guid encounterId, IReadOnlyDictionary<string, string> respostas, string? ip,
        CancellationToken ct = default)
    {
        var p = await db.PesquisasSatisfacao
                    .FirstOrDefaultAsync(x => x.EncounterId == encounterId && x.PatientId == pacienteId, ct)
                ?? await CriarAsync(pacienteId, encounterId, ct);

        Gravar(p, respostas, ip);
        await db.SaveChangesAsync(ct);
    }

    public async Task<EnvioPesquisaDto> PrepararEnvioAsync(
        Guid pacienteId, Guid encounterId, CancellationToken ct = default)
    {
        var p = await db.PesquisasSatisfacao.FirstOrDefaultAsync(x => x.EncounterId == encounterId, ct);
        var jaEnviada = p?.EnviadaEm is not null;

        if (p is null)
        {
            p = await CriarAsync(pacienteId, encounterId, ct);
        }
        else if (p.PatientId != pacienteId)
        {
            // O atendimento é de outra pessoa: enviar aqui mandaria o convite ao paciente errado.
            throw new ConflitoException(
                "pesquisa.paciente_divergente",
                "Este atendimento pertence a outro paciente.");
        }

        if (DateTime.UtcNow > p.ExpiraEm)
            throw new ConflitoException(
                "pesquisa.fora_da_janela",
                $"O prazo de {IPesquisasSatisfacaoService.JanelaDias} dias para avaliar este atendimento já passou.");

        if (p.RespondidaEm is not null)
            throw new ConflitoException("pesquisa.ja_respondida", "Este atendimento já foi avaliado.");

        p.EnviadaEm = DateTime.UtcNow;
        p.EnviadaPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);

        return new EnvioPesquisaDto(p.Id, MontarUrl(p.Id), jaEnviada);
    }

    /// <summary>
    /// Cria a pesquisa a partir do atendimento no hub. Passa pelo serviço de atendimentos do
    /// PACIENTE de propósito: é o que garante que ninguém abra pesquisa de encontro alheio.
    /// </summary>
    private async Task<PesquisaSatisfacao> CriarAsync(Guid pacienteId, Guid encounterId, CancellationToken ct)
    {
        var lista = await atendimentos.ObterPorPacienteAsync(pacienteId, ct);
        var a = lista.FirstOrDefault(x => x.Id == encounterId)
            ?? throw new NaoEncontradoException("Atendimento", encounterId);

        // O fim do atendimento é o marco. Sem ele o atendimento não terminou — e não há o que avaliar.
        var fim = (a.Fim ?? a.Inicio)?.UtcDateTime
            ?? throw new ConflitoException(
                "pesquisa.atendimento_sem_data", "Este atendimento ainda não tem data de saída.");

        var p = new PesquisaSatisfacao
        {
            Id = Guid.CreateVersion7(),
            PatientId = pacienteId,
            EncounterId = encounterId,
            AtendimentoEm = fim,
            ExpiraEm = fim.AddDays(IPesquisasSatisfacaoService.JanelaDias),
            InstrumentoVersao = IPesquisasSatisfacaoService.InstrumentoVersaoAtual,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.PesquisasSatisfacao.Add(p);
        return p;
    }

    /// <summary>Régua única de gravação — as duas portas (link e app) passam por aqui.</summary>
    private static void Gravar(PesquisaSatisfacao p, IReadOnlyDictionary<string, string> respostas, string? ip)
    {
        if (p.RespondidaEm is not null)
            throw new ConflitoException("pesquisa.ja_respondida", "Esta pesquisa já foi respondida.");

        if (DateTime.UtcNow > p.ExpiraEm)
            throw new ConflitoException(
                "pesquisa.fora_da_janela",
                $"O prazo de {IPesquisasSatisfacaoService.JanelaDias} dias para responder já passou.");

        var desconhecidas = respostas.Keys.Where(k => !PerguntasValidas.Contains(k)).ToList();
        if (desconhecidas.Count > 0)
            throw new ValidacaoException(
                "pesquisa.pergunta_desconhecida",
                $"Pergunta(s) não reconhecida(s): {string.Join(", ", desconhecidas)}.");

        // A avaliação geral é a única obrigatória — é dela que sai o índice da unidade.
        if (!respostas.TryGetValue("geral", out var geral) || string.IsNullOrWhiteSpace(geral))
            throw new ValidacaoException(
                "pesquisa.avaliacao_geral_obrigatoria", "A avaliação geral é obrigatória.");

        var limpas = respostas
            .Where(r => !string.IsNullOrWhiteSpace(r.Value))
            .ToDictionary(
                r => r.Key,
                r => r.Key == "comentario" && r.Value.Length > TamanhoMaximoComentario
                    ? r.Value[..TamanhoMaximoComentario]
                    : r.Value.Trim());

        p.RespostasJson = JsonSerializer.Serialize(limpas);
        p.RespondidaEm = DateTime.UtcNow;
        p.RespondidaIp = ip;
    }

    private string MontarUrl(Guid token)
    {
        var appBase = (configuration["Publico:AppBaseUrl"] ?? "https://app.smsmarica.online").TrimEnd('/');
        return $"{appBase}/pesquisa/{token}";
    }
}

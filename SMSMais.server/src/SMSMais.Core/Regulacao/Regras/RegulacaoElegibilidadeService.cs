using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Cidadao;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Regulacao.Anexos;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Regras;

public interface IRegulacaoElegibilidadeService
{
    /// <summary>Avalia e <b>persiste</b> o resultado: respostas deduzidas, destinos e caixinhas.</summary>
    Task<AvaliacaoElegibilidadeDto> AvaliarAsync(Guid solicitacaoId, CancellationToken ct);

    Task<AvaliacaoElegibilidadeDto> ResponderAsync(
        Guid solicitacaoId, IReadOnlyDictionary<Guid, RespostaRegraRegulacao> respostas, CancellationToken ct);

    /// <summary>Exames do próprio SMSMais que servem para aquela caixinha.</summary>
    Task<IReadOnlyList<ExameParaRegras>> ExamesInternosAsync(
        Guid solicitacaoId, Guid exigenciaId, CancellationToken ct);

    /// <summary>
    /// Usa um exame que o SMSMais já tem como resposta à exigência (R-09): gera o PDF, anexa na
    /// caixinha e registra quem validou que aquele exame é o pedido.
    /// </summary>
    Task<ExigenciaDto> UsarExameInternoAsync(
        Guid solicitacaoId, Guid exigenciaId, Guid exameId, Guid? laudoId, CancellationToken ct);
}

/// <summary>
/// Junta o que o motor precisa — paciente, regras, respostas, exames — e guarda o veredito
/// (plano 03).
///
/// <para><b>O motor decide; este serviço busca e persiste.</b> A separação é o que permite testar
/// a régua clínica sem banco e, aqui, cuidar só de onde cada dado mora.</para>
///
/// <para><b>Avaliar é idempotente e reescreve o veredito.</b> Reavaliar depois de o solicitante
/// responder uma pergunta tem de mudar o destino — acumular avaliações antigas faria a tela
/// mostrar "bloqueado" e "liberado" ao mesmo tempo.</para>
/// </summary>
public sealed class RegulacaoElegibilidadeService(
    SmsMaisDbContext db,
    IRegulacaoEscopo escopoRegulacao,
    IRegulacaoConfiguracaoService configuracao,
    IRegulacaoExigenciaService exigencias,
    IPacientesService pacientes,
    ICidadaoClinicoService clinico,
    IUsuarioAtualAccessor usuarioAtual) : IRegulacaoElegibilidadeService
{
    public Task<AvaliacaoElegibilidadeDto> AvaliarAsync(Guid solicitacaoId, CancellationToken ct) =>
        AvaliarInternoAsync(solicitacaoId, null, ct);

    public Task<AvaliacaoElegibilidadeDto> ResponderAsync(
        Guid solicitacaoId,
        IReadOnlyDictionary<Guid, RespostaRegraRegulacao> respostas,
        CancellationToken ct) =>
        AvaliarInternoAsync(solicitacaoId, respostas, ct);

    private async Task<AvaliacaoElegibilidadeDto> AvaliarInternoAsync(
        Guid solicitacaoId,
        IReadOnlyDictionary<Guid, RespostaRegraRegulacao>? novasRespostas,
        CancellationToken ct)
    {
        var s = await CarregarAsync(solicitacaoId, ct);
        var config = await configuracao.ObterEntidadeAsync(ct);

        var regras = await RegrasDoProcedimentoAsync(s.ProcedimentoId, ct);
        var paciente = await PacienteAsync(s.PacienteId, ct);
        var exames = await ExamesAsync(s.PacienteId, ct);
        var candidatos = await CandidatosAsync(s, ct);
        var cid = CidDaSolicitacao(s.FormularioJson);

        // As respostas já dadas continuam valendo; as novas entram por cima. Sem isso, responder
        // uma pergunta apagaria as anteriores e o solicitante recomeçaria o questionário.
        var respostas = await db.RegulacaoSolicitacaoRespostasRegra.AsNoTracking()
            .Where(r => r.SolicitacaoId == solicitacaoId && r.Resposta != RespostaRegraRegulacao.Deduzido)
            .ToDictionaryAsync(r => r.RegraId, r => r.Resposta, ct);

        if (novasRespostas is not null)
        {
            foreach (var (regraId, resposta) in novasRespostas) respostas[regraId] = resposta;
        }

        var avaliacao = AvaliadorElegibilidade.Avaliar(
            regras, paciente, cid, respostas, exames, candidatos,
            DateOnly.FromDateTime(DateTime.UtcNow), config.NaoSeiPadrao);

        await PersistirAsync(s, regras, avaliacao, respostas, ct);
        return avaliacao;
    }

    public async Task<IReadOnlyList<ExameParaRegras>> ExamesInternosAsync(
        Guid solicitacaoId, Guid exigenciaId, CancellationToken ct)
    {
        var s = await CarregarAsync(solicitacaoId, ct);

        var regraId = await db.RegulacaoSolicitacaoExigencias.AsNoTracking()
            .Where(e => e.Id == exigenciaId && e.SolicitacaoId == solicitacaoId)
            .Select(e => e.RegraId)
            .FirstOrDefaultAsync(ct);
        if (regraId is null) return [];

        var regra = await db.RegulacaoRegras.AsNoTracking().FirstOrDefaultAsync(r => r.Id == regraId, ct);
        if (regra?.TipoExameId is null) return [];

        var exames = await ExamesAsync(s.PacienteId, ct);
        var limite = regra.ValidadeDias is { } dias
            ? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-dias)
            : DateOnly.MinValue;

        return [.. exames
            .Where(e => e.TipoExameId == regra.TipoExameId && e.RealizadoEm >= limite)
            .OrderByDescending(e => e.Laudado).ThenByDescending(e => e.RealizadoEm)];
    }

    public async Task<ExigenciaDto> UsarExameInternoAsync(
        Guid solicitacaoId, Guid exigenciaId, Guid exameId, Guid? laudoId, CancellationToken ct)
    {
        var s = await CarregarAsync(solicitacaoId, ct);

        var exigencia = await db.RegulacaoSolicitacaoExigencias
            .FirstOrDefaultAsync(e => e.Id == exigenciaId && e.SolicitacaoId == solicitacaoId, ct)
            ?? throw new NaoEncontradoException("Exigência da solicitação", exigenciaId);

        // O exame tem de ser DAQUELE paciente. A checagem é do serviço clínico, que já resolve
        // conciliação por UID — refazer a conta aqui deixaria uma porta por onde o exame de
        // outra pessoa entraria na solicitação.
        var candidatos = await ExamesAsync(s.PacienteId, ct);
        var exame = candidatos.FirstOrDefault(e => e.Id == exameId)
            ?? throw new ValidacaoException(
                "exame", "Este exame não é do paciente da solicitação.");

        var idDoLaudo = laudoId ?? exame.LaudoId;

        // Prefere o laudo: é o que a regulação lê. Sem laudo, vai o PDF das imagens — serve de
        // comprovação de que o exame foi feito, que já é mais do que anexo nenhum.
        byte[]? conteudo = null;
        var nome = $"{exame.Descricao} - {exame.RealizadoEm:dd-MM-yyyy}.pdf";

        if (idDoLaudo is { } lid)
        {
            var pdf = await clinico.ObterLaudoPdfAsync(s.PacienteId, lid, ct);
            conteudo = pdf?.Conteudo;
            if (pdf is not null) nome = $"Laudo - {nome}";
        }

        conteudo ??= await clinico.ObterImagensPdfAsync(s.PacienteId, exameId, ct);

        if (conteudo is null || conteudo.Length == 0)
        {
            throw new ValidacaoException(
                "exame",
                "Não foi possível gerar o PDF deste exame. Anexe o arquivo manualmente.");
        }

        await exigencias.AnexarInternoAsync(
            solicitacaoId, exigenciaId, nome, "application/pdf", conteudo, ct);

        exigencia.Situacao = SituacaoExigenciaRegulacao.Atendida;
        exigencia.ExameInternoExameImagemId = exameId;
        exigencia.ExameInternoLaudoId = idDoLaudo;

        // Quem validou que aquele exame é o pedido é uma pessoa, e isso fica registrado: o
        // sistema ofereceu, alguém confirmou.
        exigencia.ValidadoExamePor = usuarioAtual.UsuarioId;
        exigencia.ValidadoExameEm = DateTime.UtcNow;
        exigencia.CriticaTexto = null;
        await db.SaveChangesAsync(ct);

        return (await exigencias.ListarAsync(solicitacaoId, ct)).First(e => e.Id == exigenciaId);
    }

    // ---------------------------------------------------------------- apoio

    private async Task<RegulacaoSolicitacao> CarregarAsync(Guid id, CancellationToken ct)
    {
        var escopo = await escopoRegulacao.ResolverAsync(ct);
        var s = await db.RegulacaoSolicitacoes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Solicitação da regulação", id);

        if (!escopo.VeTudo && !escopo.Unidades.Contains(s.UnidadeSolicitanteId))
        {
            throw new NaoEncontradoException("Solicitação da regulação", id);
        }
        return s;
    }

    private async Task<IReadOnlyList<RegulacaoRegra>> RegrasDoProcedimentoAsync(
        Guid procedimentoId, CancellationToken ct) =>
        await db.RegulacaoRegras.AsNoTracking()
            .Where(r => r.ProcedimentoId == procedimentoId && r.Ativo)
            .OrderBy(r => r.Ordem)
            .ToListAsync(ct);

    private async Task<PacienteParaRegras> PacienteAsync(Guid pacienteId, CancellationToken ct)
    {
        var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
        return new PacienteParaRegras(
            p.DataNascimento,
            // O cadastro usa o enum FHIR; a regra do manual fala em "M"/"F". `Outro` e
            // `NaoInformado` viram null de propósito: uma regra de sexo sobre eles tem de ficar
            // indefinida e ser conferida por gente, não decidida por aproximação.
            p.Sexo switch
            {
                Sexo.Masculino => "M",
                Sexo.Feminino => "F",
                _ => null,
            },
            string.IsNullOrWhiteSpace(p.Cpf) ? null : p.Cpf,
            null);
    }

    /// <summary>
    /// Os exames do paciente, com o tipo.
    ///
    /// <para>Reusa <see cref="ICidadaoClinicoService.ListarExamesAsync"/> — que já sabe o que
    /// conta como exame do paciente, inclusive a conciliação por UID — e complementa com o
    /// <c>TipoExameId</c>, que aquele DTO não carrega, numa consulta só.</para>
    /// </summary>
    private async Task<IReadOnlyList<ExameParaRegras>> ExamesAsync(Guid pacienteId, CancellationToken ct)
    {
        var resumos = await clinico.ListarExamesAsync(pacienteId, ct);
        if (resumos.Count == 0) return [];

        var ids = resumos.Select(e => e.Id).ToArray();
        var tipos = await db.ExamesImagem.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .Select(e => new { e.Id, e.TipoExameId })
            .ToDictionaryAsync(e => e.Id, e => e.TipoExameId, ct);

        return [.. resumos.Select(e => new ExameParaRegras(
            e.Id,
            tipos.GetValueOrDefault(e.Id),
            DateOnly.FromDateTime(e.Data),
            e.LaudoId is not null,
            e.Nome,
            e.LaudoId))];
    }

    /// <summary>
    /// Para onde este pedido pode ir, antes das regras. Interno e NAR terminam no SISREG (D-9);
    /// o Externo depende de onde o procedimento existe.
    /// </summary>
    private async Task<IReadOnlyList<SistemaRegulacao>> CandidatosAsync(
        RegulacaoSolicitacao s, CancellationToken ct)
    {
        if (s.Fluxo is FluxoRegulacao.Interno or FluxoRegulacao.Nar) return [SistemaRegulacao.Sisreg];

        var comOrigem = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.ProcedimentoId == s.ProcedimentoId && o.Ativo && o.Sistema != SistemaRegulacao.Sisreg)
            .Select(o => o.Sistema)
            .Distinct()
            .ToListAsync(ct);

        return comOrigem;
    }

    /// <summary>O CID que a hipótese informou — o formulário canônico é quem o carrega.</summary>
    private static string? CidDaSolicitacao(string formularioJson)
    {
        try
        {
            var raiz = JsonDocument.Parse(formularioJson).RootElement;
            if (!raiz.TryGetProperty("canonico", out var c)) return null;

            foreach (var chave in new[] { "cid10", "cid", "cid_principal" })
            {
                if (c.TryGetProperty(chave, out var v) && v.ValueKind == JsonValueKind.String)
                {
                    var texto = v.GetString();
                    if (!string.IsNullOrWhiteSpace(texto)) return texto;
                }
            }
        }
        catch (JsonException)
        {
            // Formulário ilegível não pode derrubar a avaliação: segue sem CID.
        }
        return null;
    }

    private async Task PersistirAsync(
        RegulacaoSolicitacao s,
        IReadOnlyList<RegulacaoRegra> regras,
        AvaliacaoElegibilidadeDto avaliacao,
        IReadOnlyDictionary<Guid, RespostaRegraRegulacao> respostas,
        CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var usuarioId = usuarioAtual.UsuarioId;

        var existentes = await db.RegulacaoSolicitacaoRespostasRegra
            .Where(r => r.SolicitacaoId == s.Id)
            .ToDictionaryAsync(r => r.RegraId, ct);
        var porId = regras.ToDictionary(r => r.Id);

        foreach (var avaliada in avaliacao.Regras)
        {
            if (!porId.TryGetValue(avaliada.RegraId, out var regra)) continue;

            var resposta = respostas.GetValueOrDefault(avaliada.RegraId, RespostaRegraRegulacao.Deduzido);

            if (existentes.TryGetValue(avaliada.RegraId, out var linha))
            {
                linha.RegraVersao = regra.Versao;
                linha.Resposta = resposta;
                linha.Resultado = avaliada.Resultado;
                linha.ValorDeduzido = avaliada.Motivo;
                linha.RespondidoEm = agora;
                linha.RespondidoPor = usuarioId;
                continue;
            }

            db.RegulacaoSolicitacaoRespostasRegra.Add(new RegulacaoSolicitacaoRespostaRegra
            {
                Id = Guid.CreateVersion7(),
                SolicitacaoId = s.Id,
                RegraId = avaliada.RegraId,
                RegraVersao = regra.Versao,
                Resposta = resposta,
                Resultado = avaliada.Resultado,
                ValorDeduzido = avaliada.Motivo,
                RespondidoEm = agora,
                RespondidoPor = usuarioId,
            });
        }

        // Os destinos são reescritos por inteiro: um veredito velho ao lado do novo faria a tela
        // mostrar "bloqueado" e "liberado" para o mesmo sistema.
        var destinos = await db.RegulacaoSolicitacaoDestinos
            .Where(d => d.SolicitacaoId == s.Id).ToListAsync(ct);
        db.RegulacaoSolicitacaoDestinos.RemoveRange(destinos);

        foreach (var sistema in avaliacao.DestinosPermitidos.Concat(avaliacao.MotivosDeBloqueio.Keys).Distinct())
        {
            var bloqueado = avaliacao.MotivosDeBloqueio.TryGetValue(sistema, out var motivo);
            db.RegulacaoSolicitacaoDestinos.Add(new RegulacaoSolicitacaoDestino
            {
                Id = Guid.CreateVersion7(),
                SolicitacaoId = s.Id,
                Sistema = sistema,
                Situacao = bloqueado
                    ? SituacaoDestinoRegulacao.Bloqueado
                    : avaliacao.DestinosComRessalva.Contains(sistema)
                        ? SituacaoDestinoRegulacao.ComRessalva
                        : SituacaoDestinoRegulacao.Elegivel,
                Motivo = motivo,
                AvaliadoEm = agora,
            });
        }

        await db.SaveChangesAsync(ct);

        // As caixinhas nascem depois da gravação: `GarantirDaRegraAsync` tem o próprio
        // `SaveChanges`, e uma exigência criada para uma regra que não persistiu ficaria órfã.
        foreach (var documento in avaliacao.DocumentosPendentes)
        {
            await exigencias.GarantirDaRegraAsync(
                s.Id, documento.RegraId, documento.Rotulo, documento.Obrigatorio, ct);
        }
    }
}

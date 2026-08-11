using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Auditoria;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Notificacoes.Comunicacao;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Core.Pacs;
using SMSMarica.Core.SolicitacoesExame.Identificadores;
using SMSMarica.Core.Worklist;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Associacoes;

/// <inheritdoc cref="ICorrecaoIdentidadeExameService"/>
public sealed class CorrecaoIdentidadeExameService(
    SmsMaricaDbContext db,
    IPacsReescritorEstudoClient reescritor,
    IResolvedorIdentidadeDicom identidades,
    IPacienteResolver pacientes,
    IDcm4cheeMwlClient mwl,
    IGeradorIdentificadores identificadores,
    IComunicacaoPacienteService comunicacoes,
    IQuarentenaIdentidadeService quarentena,
    IAuditoriaService auditoria,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<CorrecaoIdentidadeExameService> logger) : ICorrecaoIdentidadeExameService
{
    public async Task<PreviaCorrecaoDto> ObterPreviaAsync(
        string studyInstanceUID, string? accessionDestino, CancellationToken cancellationToken = default)
    {
        var uid = Exigir(studyInstanceUID, "correcao.study_obrigatorio", "StudyInstanceUID é obrigatório.");

        var atual = await ExamePorStudyAsync(uid, cancellationToken);
        var nomeAtual = atual is null ? null : await NomeDoPacienteAsync(atual, cancellationToken);

        var rascunhos = await db.Laudos.CountAsync(
            l => l.StudyInstanceUID == uid && !l.Excluido && l.ExcluidoEm == null, cancellationToken);
        var assinado = await TemLaudoAssinadoAsync(uid, cancellationToken);

        var enviada = atual is not null && await db.ComunicacoesPaciente.AnyAsync(
            c => c.SolicitacaoId == atual.SolicitacaoId && c.EnviadoEm != null, cancellationToken);

        if (string.IsNullOrWhiteSpace(accessionDestino))
        {
            return new PreviaCorrecaoDto(uid, nomeAtual, atual?.Id, atual?.AccessionNumber,
                string.Empty, Guid.Empty, string.Empty, null, rascunhos, assinado, enviada, null);
        }

        var destino = await ExamePorAccessionAsync(accessionDestino.Trim(), cancellationToken);
        var nomeDestino = await NomeDoPacienteAsync(destino, cancellationToken) ?? "(sem nome)";

        return new PreviaCorrecaoDto(
            uid, nomeAtual, atual?.Id, atual?.AccessionNumber,
            nomeDestino, destino.Id, destino.AccessionNumber,
            destino.Solicitacao?.ProcedimentoTexto,
            rascunhos, assinado, enviada,
            await SugerirStudyDoDestinoAsync(destino, uid, cancellationToken));
    }

    public async Task DescartarEstudoAsync(
        DescartarEstudoRequest request, CancellationToken cancellationToken = default)
    {
        var uid = Exigir(request.StudyInstanceUID, "correcao.study_obrigatorio", "StudyInstanceUID é obrigatório.");
        var motivo = ExigirMotivo(request.Motivo);
        await GarantirSemLaudoAssinadoAsync(uid, cancellationToken);

        var exame = await ExamePorStudyAsync(uid, cancellationToken);
        var antes = await DescreverAsync(exame, uid, cancellationToken);

        // PACS primeiro (régua dcm4chee-first do repo): se ele recusar, nada muda aqui.
        await reescritor.DescartarAsync(uid, cancellationToken);

        if (exame is not null)
        {
            // Normalmente já não há item (o exame estava Realizada e o worker o retirou), mas
            // remover é no-op quando não há — e garante que nenhum item fique com o UID que sai.
            await RemoverDaWorklistAsync(exame, cancellationToken);
            await DescartarRascunhosAsync(uid, cancellationToken);
            await RevogarComunicacaoAsync(exame, cancellationToken);
            await DevolverAWorklistAsync(exame, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        await quarentena.ResolverPorEstudoAsync(uid, $"Estudo descartado. {motivo}", cancellationToken);
        await AuditarAsync("DescarteDeEstudo", exame, antes,
            $"Estudo {uid} rejeitado (IOCM 113038) e apagado do PACS. Motivo: {motivo}", cancellationToken);
        logger.LogInformation("Correção de identidade: estudo {Uid} descartado. Motivo: {Motivo}", uid, motivo);
    }

    public async Task AlterarDestinoAsync(
        AlterarDestinoRequest request, CancellationToken cancellationToken = default)
    {
        var uid = Exigir(request.StudyInstanceUID, "correcao.study_obrigatorio", "StudyInstanceUID é obrigatório.");
        var motivo = ExigirMotivo(request.Motivo);
        await GarantirSemLaudoAssinadoAsync(uid, cancellationToken);

        var origem = await ExamePorStudyAsync(uid, cancellationToken);
        var destino = await ExamePorAccessionAsync(
            Exigir(request.AccessionDestino, "correcao.destino_obrigatorio", "Informe o nº da solicitação de destino."),
            cancellationToken);

        if (origem is not null && origem.Id == destino.Id)
            throw new ConflitoException("correcao.destino_igual_origem",
                "O estudo já pertence a esta solicitação — não há o que corrigir.");
        GarantirDestinoElegivel(destino);

        var antes = await DescreverAsync(origem, uid, cancellationToken);
        var uidNovo = await ReescreverParaAsync(uid, destino, cancellationToken);

        // Item de worklist da ORIGEM sai antes de ela largar o UID — foi com o UID atual que ele
        // foi registrado no dcm4chee. Fora da transação: é chamada de rede.
        if (origem is not null) await RemoverDaWorklistAsync(origem, cancellationToken);

        // A partir daqui o PACS já está correto. O banco acompanha numa transação só.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

            await DescartarRascunhosAsync(uid, cancellationToken);
            if (origem is not null)
            {
                await RevogarComunicacaoAsync(origem, cancellationToken);
                // ORDEM OBRIGATÓRIA: a origem larga o UID (com SaveChanges) ANTES de o destino
                // assumi-lo. study_instance_uid é UNIQUE — inverter aqui viola o índice.
                if (request.DestinoDaOrigem == DestinoDoExameDeOrigem.DevolverAWorklist)
                    await DevolverAWorklistAsync(origem, cancellationToken);
                else
                    await LiberarSemWorklistAsync(origem, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }

            await AssumirEstudoAsync(destino, uidNovo, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });

        await RemoverDaWorklistAsync(destino, cancellationToken);

        await quarentena.ResolverPorEstudoAsync(uid, $"Estudo passou para {destino.AccessionNumber}. {motivo}", cancellationToken);
        await AuditarAsync("AlteracaoDeDestinoDeEstudo", origem, antes,
            $"Estudo reescrito para {destino.AccessionNumber} (novo UID {uidNovo}). " +
            $"Origem: {(request.DestinoDaOrigem == DestinoDoExameDeOrigem.DevolverAWorklist ? "devolvida à worklist" : "liberada sem worklist")}. " +
            $"Motivo: {motivo}", cancellationToken);
    }

    public async Task TrocarAsync(TrocarEstudosRequest request, CancellationToken cancellationToken = default)
    {
        var uid = Exigir(request.StudyInstanceUID, "correcao.study_obrigatorio", "StudyInstanceUID é obrigatório.");
        var uidDestino = Exigir(request.StudyInstanceUIDDoDestino,
            "correcao.study_destino_obrigatorio", "Informe o estudo que volta para a origem.");
        var motivo = ExigirMotivo(request.Motivo);
        if (string.Equals(uid, uidDestino, StringComparison.Ordinal))
            throw new ValidacaoException("correcao.studies_iguais", "Os dois estudos da troca são o mesmo.");

        await GarantirSemLaudoAssinadoAsync(uid, cancellationToken);
        await GarantirSemLaudoAssinadoAsync(uidDestino, cancellationToken);

        var origem = await ExamePorStudyAsync(uid, cancellationToken)
            ?? throw new ConflitoException("correcao.origem_sem_exame",
                "O estudo não está vinculado a exame nenhum — use 'alterar destino'.");
        var destino = await ExamePorAccessionAsync(
            Exigir(request.AccessionDestino, "correcao.destino_obrigatorio", "Informe o nº da solicitação de destino."),
            cancellationToken);
        if (origem.Id == destino.Id)
            throw new ConflitoException("correcao.destino_igual_origem", "Origem e destino são o mesmo exame.");
        GarantirDestinoElegivel(destino);

        var antes = $"{await DescreverAsync(origem, uid, cancellationToken)} | destino tinha {uidDestino}";

        // Reescreve os DOIS antes de tocar o banco: se o segundo falhar, o primeiro já está
        // corrigido no PACS e a operação pode ser retomada — nunca fica pior que o estado inicial.
        var novoParaDestino = await ReescreverParaAsync(uid, destino, cancellationToken);
        var novoParaOrigem = await ReescreverParaAsync(uidDestino, origem, cancellationToken);

        // Os dois largam os UIDs, então os dois itens de worklist saem antes (chamadas de rede,
        // fora da transação). Ninguém volta à worklist: os dois exames foram feitos.
        await RemoverDaWorklistAsync(origem, cancellationToken);
        await RemoverDaWorklistAsync(destino, cancellationToken);

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

            await DescartarRascunhosAsync(uid, cancellationToken);
            await DescartarRascunhosAsync(uidDestino, cancellationToken);
            await RevogarComunicacaoAsync(origem, cancellationToken);
            await RevogarComunicacaoAsync(destino, cancellationToken);

            // Os dois largam os UIDs antigos primeiro (índice único), depois assumem os novos.
            await LiberarSemWorklistAsync(origem, cancellationToken);
            await LiberarSemWorklistAsync(destino, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            await AssumirEstudoAsync(destino, novoParaDestino, cancellationToken);
            await AssumirEstudoAsync(origem, novoParaOrigem, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });

        await quarentena.ResolverPorEstudoAsync(uid, $"Troca concluída. {motivo}", cancellationToken);
        await quarentena.ResolverPorEstudoAsync(uidDestino, $"Troca concluída. {motivo}", cancellationToken);
        await AuditarAsync("TrocaDeIdentidadeDeExame", origem, antes,
            $"{origem.AccessionNumber} ficou com {novoParaOrigem}; {destino.AccessionNumber} ficou com " +
            $"{novoParaDestino}. Motivo: {motivo}", cancellationToken);
    }

    // ---- passos compartilhados ----

    /// <summary>Reescreve o estudo com a identidade do exame de destino e devolve o UID novo.</summary>
    private async Task<string> ReescreverParaAsync(string uid, ExameImagem destino, CancellationToken ct)
    {
        var identidade = await identidades.ObterAsync(destino.Id, ct);
        var resultado = await reescritor.ReescreverIdentidadeAsync(uid, identidade, ct);
        return resultado.StudyInstanceUIDNovo;
    }

    /// <summary>O exame passa a ser dono do estudo e vira Realizada, com a data real do DICOM.</summary>
    private async Task AssumirEstudoAsync(ExameImagem exame, string uidNovo, CancellationToken ct)
    {
        var alvo = await db.ExamesImagem.FirstAsync(e => e.Id == exame.Id, ct);
        alvo.StudyInstanceUID = uidNovo;
        alvo.Status = StatusSolicitacaoExame.Realizada;
        alvo.RealizadoEm ??= DateTime.UtcNow;
        alvo.ProximaTentativaEm = null;
        alvo.AtualizadoEm = DateTime.UtcNow;
        alvo.AtualizadoPor = usuarioAtual.UsuarioId;

        // Qualquer associação explícita ativa do estudo antigo perde o sentido.
        await db.ExameAssociacoes
            .Where(a => a.ExameImagemId == exame.Id && a.ExcluidoEm == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(a => a.ExcluidoEm, DateTime.UtcNow)
                .SetProperty(a => a.ExcluidoPor, usuarioAtual.UsuarioId), ct);
    }

    /// <summary>Solta o exame do estudo e o recoloca na fila da worklist, com UID NOVO — o antigo
    /// ficou colado ao estudo que saiu do PACS.
    ///
    /// <para>Pressupõe o item antigo já removido pelo chamador. Se ficasse, o worker criaria um
    /// SEGUNDO item (com o UID novo) e o paciente apareceria duas vezes na estação — e o antigo
    /// nunca mais sairia, porque o espelho <c>WorklistItemUid</c> passa a apontar para o novo.</para></summary>
    private async Task DevolverAWorklistAsync(ExameImagem exame, CancellationToken ct)
    {
        var alvo = await db.ExamesImagem.FirstAsync(e => e.Id == exame.Id, ct);
        alvo.StudyInstanceUID = identificadores.NovoStudyInstanceUid();
        alvo.Status = StatusSolicitacaoExame.Solicitada;
        alvo.WorklistItemUid = null;
        alvo.RealizadoEm = null;
        alvo.DataEstudo = null;
        alvo.TentativasEnvio = 0;
        alvo.ErroIntegracaoPacs = null;
        // O worker de worklist (ciclo de 15s) recria o item. NÃO precisa reautorizar na recepção:
        // a autorização vive na Solicitacao e governa só o enfileiramento inicial.
        alvo.ProximaTentativaEm = DateTime.UtcNow;
        alvo.AtualizadoEm = DateTime.UtcNow;
        alvo.AtualizadoPor = usuarioAtual.UsuarioId;
    }

    /// <summary>Solta o exame do estudo SEM recriar item de worklist — o exame já foi feito e vai
    /// ser resolvido por associação. Evita o paciente reaparecer na estação no meio do caminho.
    ///
    /// <para>Pressupõe que o item de worklist ANTIGO já foi removido do dcm4chee pelo chamador
    /// (<see cref="RemoverDaWorklistAsync"/>, antes da transação). O estado que sobra
    /// (<c>Recebida</c>) não é pego por nenhuma das duas filas do <c>EnviadorWorklistService</c> —
    /// a de limpeza só olha Realizada/Laudada/Cancelada/excluído e a de envio não mexe em
    /// Recebida —, então um item deixado para trás ficaria órfão na estação para sempre.</para></summary>
    private async Task LiberarSemWorklistAsync(ExameImagem exame, CancellationToken ct)
    {
        var alvo = await db.ExamesImagem.FirstAsync(e => e.Id == exame.Id, ct);
        alvo.StudyInstanceUID = identificadores.NovoStudyInstanceUid();
        alvo.Status = StatusSolicitacaoExame.Recebida;
        alvo.WorklistItemUid = null;
        alvo.RealizadoEm = null;
        alvo.DataEstudo = null;
        alvo.ProximaTentativaEm = null;
        alvo.AtualizadoEm = DateTime.UtcNow;
        alvo.AtualizadoPor = usuarioAtual.UsuarioId;
    }

    /// <summary>Tira o item pendente da tela do equipamento — o exame já existe.</summary>
    private async Task RemoverDaWorklistAsync(ExameImagem exame, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(exame.WorklistItemUid)) return;
        try
        {
            await mwl.ExcluirMwlItemAsync(exame, ct);
            await db.ExamesImagem.Where(e => e.Id == exame.Id)
                .ExecuteUpdateAsync(u => u.SetProperty(e => e.WorklistItemUid, (string?)null), ct);
        }
        catch (Exception ex)
        {
            // Best-effort, como o cancelamento: a fila de limpeza do worker retenta sozinha.
            logger.LogWarning(ex, "Falha ao remover o item de worklist do exame {Id}.", exame.Id);
        }
    }

    /// <summary>Rascunhos foram escritos sobre as imagens de outra pessoa — não se aproveitam.
    /// Soft-delete; laudo ASSINADO nunca chega aqui (é barrado antes).</summary>
    private async Task DescartarRascunhosAsync(string uid, CancellationToken ct) =>
        await db.Laudos
            .Where(l => l.StudyInstanceUID == uid && !l.Excluido && l.ExcluidoEm == null)
            .ExecuteUpdateAsync(u => u
                .SetProperty(l => l.Excluido, true)
                .SetProperty(l => l.ExcluidoEm, DateTime.UtcNow)
                .SetProperty(l => l.ExcluidoPorUsuarioId, usuarioAtual.UsuarioId), ct);

    /// <summary>Corta o link já enviado e cancela o aviso pendente do exame que perdeu o estudo.</summary>
    private async Task RevogarComunicacaoAsync(ExameImagem exame, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        await comunicacoes.RevogarAcessosAsync(exame.SolicitacaoId, agora, ct);
        await db.ComunicacoesPaciente
            .Where(c => c.SolicitacaoId == exame.SolicitacaoId
                        && c.Finalidade == FinalidadeComunicacao.ExameLiberado
                        && c.Status == StatusComunicacao.Pendente)
            .ExecuteUpdateAsync(u => u
                .SetProperty(c => c.Status, StatusComunicacao.Falha)
                .SetProperty(c => c.MotivoFalha, "Cancelada por correção de identidade do exame.")
                .SetProperty(c => c.ProximaTentativaEm, (DateTime?)null)
                .SetProperty(c => c.AtualizadoEm, agora), ct);
    }

    // ---- consultas e validações ----

    private async Task<ExameImagem?> ExamePorStudyAsync(string uid, CancellationToken ct)
    {
        // Vínculo implícito (coluna do exame) primeiro; depois o explícito.
        var porColuna = await db.ExamesImagem.AsNoTracking().Include(e => e.Solicitacao)
            .FirstOrDefaultAsync(e => e.StudyInstanceUID == uid && e.ExcluidoEm == null, ct);
        if (porColuna is not null) return porColuna;

        var explicito = await db.ExameAssociacoes.AsNoTracking()
            .Where(a => a.StudyInstanceUID == uid && a.ExcluidoEm == null)
            .Select(a => a.ExameImagemId)
            .FirstOrDefaultAsync(ct);
        return explicito == Guid.Empty
            ? null
            : await db.ExamesImagem.AsNoTracking().Include(e => e.Solicitacao)
                .FirstOrDefaultAsync(e => e.Id == explicito, ct);
    }

    private async Task<ExameImagem> ExamePorAccessionAsync(string accession, CancellationToken ct) =>
        await db.ExamesImagem.AsNoTracking().Include(e => e.Solicitacao)
            .FirstOrDefaultAsync(e => e.AccessionNumber == accession && e.ExcluidoEm == null, ct)
        ?? throw new NaoEncontradoException("Solicitação", accession);

    private static void GarantirDestinoElegivel(ExameImagem destino)
    {
        if (destino.Status == StatusSolicitacaoExame.Cancelada)
            throw new ConflitoException("correcao.destino_cancelado", "A solicitação de destino está cancelada.");
        if ((destino.Solicitacao?.PacienteId ?? Guid.Empty) == Guid.Empty)
            throw new ConflitoException("correcao.destino_sem_paciente",
                "A solicitação de destino não tem paciente — não há identidade para gravar no DICOM.");
    }

    private async Task GarantirSemLaudoAssinadoAsync(string uid, CancellationToken ct)
    {
        if (await TemLaudoAssinadoAsync(uid, ct))
            throw new ConflitoException("correcao.laudo_assinado",
                "Há laudo ASSINADO neste estudo. A correção de identidade exige antes a retratação do laudo.");
    }

    private async Task<bool> TemLaudoAssinadoAsync(string uid, CancellationToken ct) =>
        await db.LaudoAssinaturas.AsNoTracking()
            .AnyAsync(a => a.Status == StatusAssinatura.Concluida
                           && db.Laudos.Any(l => l.Id == a.LaudoId && l.StudyInstanceUID == uid && !l.Excluido), ct);

    /// <summary>Estudo que o destino já tem (ou órfão que parece ser dele) — sugestão da opção 3.</summary>
    private async Task<string?> SugerirStudyDoDestinoAsync(ExameImagem destino, string uidEmCorrecao, CancellationToken ct)
    {
        var proprio = await db.ExameAssociacoes.AsNoTracking()
            .Where(a => a.ExameImagemId == destino.Id && a.ExcluidoEm == null && a.StudyInstanceUID != uidEmCorrecao)
            .Select(a => a.StudyInstanceUID)
            .FirstOrDefaultAsync(ct);
        return proprio;
    }

    private async Task<string?> NomeDoPacienteAsync(ExameImagem exame, CancellationToken ct)
    {
        var id = exame.Solicitacao?.PacienteId ?? Guid.Empty;
        if (id == Guid.Empty) return null;
        return (await pacientes.ResolverAsync(id, ct))?.Nome;
    }

    private async Task<string> DescreverAsync(ExameImagem? exame, string uid, CancellationToken ct) =>
        exame is null
            ? $"Estudo {uid} sem exame vinculado"
            : $"Estudo {uid} no exame {exame.AccessionNumber} " +
              $"(paciente {await NomeDoPacienteAsync(exame, ct)}, status {exame.Status})";

    private async Task AuditarAsync(string acao, ExameImagem? exame, string antes, string depois, CancellationToken ct) =>
        await auditoria.RegistrarAsync(
            "ExameImagem", (exame?.Id ?? Guid.Empty).ToString(), acao, antes, depois, ct);

    private static string Exigir(string? valor, string codigo, string mensagem) =>
        string.IsNullOrWhiteSpace(valor) ? throw new ValidacaoException(codigo, mensagem) : valor.Trim();

    /// <summary>Motivo é obrigatório: esta operação apaga imagens do PACS e descarta laudos —
    /// a trilha precisa dizer POR QUE, não só o quê.</summary>
    private static string ExigirMotivo(string? motivo) =>
        Exigir(motivo, "correcao.motivo_obrigatorio", "Descreva o motivo da correção.");
}

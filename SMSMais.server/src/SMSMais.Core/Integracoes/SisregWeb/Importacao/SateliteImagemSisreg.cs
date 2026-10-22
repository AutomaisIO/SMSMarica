using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Identidade;
using SMSMais.Core.Solicitacoes;
using SMSMais.Core.Solicitacoes.Identificadores;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.SisregWeb.Importacao;

/// <summary>
/// Garante o satélite de execução (<see cref="ExameImagem"/>) de uma solicitação de IMAGEM que já
/// existe sem ele. A tela de Solicitações de Exame lista o EXAME, não a solicitação — pedido sem
/// satélite não aparece na busca da recepção e não há como dar entrada.
///
/// <para><b>De onde vieram:</b> a carga do histórico de 08/09/2026 gravou ~512 mil solicitações de
/// imagem sem satélite (por fora do <see cref="ImportacaoSisregService"/>), e a varredura diária,
/// ao reencontrar o número do SISREG, só reconciliava os campos. Em 05/10/2026 a recepção do CDT
/// não achava a paciente da mamografia das 08:10 — 4.292 pedidos futuros estavam invisíveis.</para>
///
/// <para><b>Invariante: TODA solicitação de categoria Imagem tem satélite.</b> A tela de Consultas
/// lista o que NÃO é imagem; a de Exames, o que tem satélite. Imagem sem satélite não aparece em
/// tela nenhuma — por isso a categoria GRAVADA manda, sem segunda opinião do SIGTAP. A 1ª versão
/// (2163681) refazia a categoria por <see cref="CategoriaSigtap"/> e deixou 1.308 pedidos da carga
/// (audiometria, EEG, cirurgias "CE-", OCI…) fora das duas telas; o Bernardo: "TODOS apareçam na
/// tela… SEMPRE".</para>
///
/// <para><b>Fora da imagem (20/10/2026):</b> Holter e MAPA são Gráfico/funcional, mas vão à worklist
/// da cardiologia e o laudo volta em PDF pelo PACS, casado pelo accession/Study — então também têm
/// satélite. O critério é o tipo de exame ter modalidade DICOM <b>e</b> a unidade executante o ter no
/// escopo com o envio à worklist ligado (<see cref="CategoriaSigtap.CriaSatelite"/>) — a ressonância
/// gravada como "Outro" numa clínica sem integração não entra; fora da imagem o tipo nunca é criado
/// aqui, só reaproveitado.</para>
///
/// <para>Espelha o caminho de criação da importação: tipo pelo NOME do SISREG
/// (<see cref="IResolvedorTipoExameSisreg"/>, nasce com worklist desligado), par tipo×unidade no
/// escopo e status <c>Solicitada</c> sem próxima tentativa — nada vai ao PACS até a recepção
/// autorizar.</para>
/// </summary>
public interface ISateliteImagemSisreg
{
    /// <summary>
    /// Cria o satélite que falta, ou completa o tipo de um satélite que nasceu sem ele — o principal
    /// e um por procedimento extra do pedido (ADR-0074). NÃO salva.
    /// <paramref name="proximoAccession"/> nulo = pede ao gerador (uso unitário, na importação);
    /// só é chamado quando um exame é de fato criado.
    /// </summary>
    Task<ResultadoSatelite> GarantirAsync(
        Solicitacao solicitacao, Func<string>? proximoAccession, CancellationToken ct);

    /// <summary>
    /// Reparo em lote, dos agendados de HOJE (Brasília) em diante: cria o satélite que falta e
    /// completa o tipo que ficou nulo. Até <paramref name="limite"/> solicitações por chamada, em
    /// ordem de id a partir de <paramref name="depoisDe"/> — quem chama repete passando o
    /// <c>Cursor</c> devolvido até <c>Restantes</c> zerar. ESCRITA.
    /// </summary>
    /// <param name="incluirAnterioresForaDaImagem">
    /// Também os agendados ANTES de hoje, mas só fora da imagem (Holter, MAPA — tipo com modalidade):
    /// é o backfill do acervo retroativo. Na imagem o corte de data continua sempre.
    /// </param>
    Task<ReparoSatelitesResultado> RepararAsync(
        int limite, Guid? depoisDe, bool incluirAnterioresForaDaImagem, CancellationToken ct);
}

public enum ResultadoSatelite
{
    NadaAFazer = 0,
    Criado = 1,
    TipoCompletado = 2,
}

public sealed record ReparoSatelitesResultado(
    int Criados,
    int TiposCompletados,
    Guid? Cursor,
    int Restantes);

public sealed class SateliteImagemSisreg(
    SmsMaisDbContext db,
    IGeradorIdentificadores geradorIds,
    IResolvedorTipoExameSisreg resolvedorTipoExame,
    SMSMais.Core.Worklist.IEscopoExameUnidade escopoExameUnidade,
    IUsuarioAtualAccessor usuarioAtual) : ISateliteImagemSisreg
{
    public async Task<ResultadoSatelite> GarantirAsync(
        Solicitacao solicitacao, Func<string>? proximoAccession, CancellationToken ct)
    {
        // A categoria não barra mais aqui: quem decide é CategoriaSigtap.CriaSatelite, via
        // ResolverParaExecucaoAsync — imagem sempre; fora dela, só tipo com modalidade que a unidade
        // executante envia à worklist (Holter, MAPA no CDT).
        if (solicitacao.Status == StatusSolicitacao.Cancelada
            || solicitacao.ExcluidoEm is not null)
            return ResultadoSatelite.NadaAFazer;

        var (resultado, principal) = await GarantirPrincipalAsync(solicitacao, proximoAccession, ct);
        var extras = await GarantirExtrasAsync(solicitacao, principal, ct);
        return resultado == ResultadoSatelite.Criado || extras == ResultadoSatelite.Criado
            ? ResultadoSatelite.Criado
            : resultado == ResultadoSatelite.TipoCompletado ? resultado : extras;
    }

    /// <summary>O exame PRINCIPAL (ordem 0): cria o que falta ou completa o tipo. Devolve também o
    /// exame, que os extras precisam para herdar o accession base.</summary>
    private async Task<(ResultadoSatelite, ExameImagem?)> GarantirPrincipalAsync(
        Solicitacao solicitacao, Func<string>? proximoAccession, CancellationToken ct)
    {
        // Sem filtro de ExcluidoEm: o índice único do principal não é filtrado — um satélite
        // excluído ainda ocupa a vaga, e criar outro estouraria.
        var exame = await db.ExamesImagem.FirstOrDefaultAsync(e => e.SolicitacaoPrincipalId == solicitacao.Id, ct);
        if (exame is not null && (exame.TipoExameId is not null || exame.ExcluidoEm is not null))
            return (ResultadoSatelite.NadaAFazer, exame);

        var nome = ResolvedorTipoExameSisreg.NormalizarNome(solicitacao.ProcedimentoTexto ?? string.Empty);
        var sig = SoDigitos(solicitacao.ProcedimentoSigtapCodigo);
        var (criaSatelite, tipoExameId) = await resolvedorTipoExame.ResolverParaExecucaoAsync(
            solicitacao.Categoria, nome, SoDigitos(solicitacao.ProcedimentoCodigoSisreg), sig,
            solicitacao.UnidadeExecutanteId, ct);
        if (exame is null && !criaSatelite) return (ResultadoSatelite.NadaAFazer, null);
        if (tipoExameId is { } tipo)
            await escopoExameUnidade.GarantirAsync(tipo, solicitacao.UnidadeExecutanteId, ct);

        var agora = DateTime.UtcNow;
        if (exame is not null)
        {
            if (tipoExameId is null) return (ResultadoSatelite.NadaAFazer, exame);
            exame.TipoExameId = tipoExameId;
            exame.AtualizadoEm = agora;
            exame.AtualizadoPor = usuarioAtual.UsuarioId;
            return (ResultadoSatelite.TipoCompletado, exame);
        }

        exame = new ExameImagem
        {
            Id = Guid.CreateVersion7(),
            SolicitacaoId = solicitacao.Id,
            SolicitacaoPrincipalId = solicitacao.Id,
            Ordem = 0,
            AccessionNumber = proximoAccession?.Invoke() ?? await geradorIds.ProximoAccessionAsync(ct),
            StudyInstanceUID = geradorIds.NovoStudyInstanceUid(),
            TipoExameId = tipoExameId, // nullable = mapeamento pendente
            Status = StatusSolicitacaoExame.Solicitada,
            // NADA vai ao PACS automaticamente: só quando a recepção AUTORIZA.
            ProximaTentativaEm = null,
            CriadoEm = agora,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.ExamesImagem.Add(exame);
        return (ResultadoSatelite.Criado, exame);
    }

    /// <summary>
    /// Um exame por procedimento EXTRA do pedido (itens de ordem ≥ 2 do ticket #149) — ADR-0074. O
    /// SISREG manda cada procedimento numa linha com o mesmo nº; cada um vira um exame com o seu
    /// tipo (o joelho E não é o joelho D), o accession do principal com sufixo (<c>-1</c>,
    /// <c>-2</c>…) e o seu próprio estudo. Completa o tipo do extra que nasceu sem ele.
    ///
    /// <para><b>Procedimento que chega depois da recepção.</b> As linhas do mesmo pedido vêm juntas
    /// (mesma data, hora, unidade e chegada em todos os 2.986 pedidos medidos), mas, se uma chegar
    /// com o pedido já autorizado, o exame dela entra na fila da worklist na hora: a recepção já
    /// liberou o pedido inteiro e não há mais ninguém para clicar.</para>
    /// </summary>
    private async Task<ResultadoSatelite> GarantirExtrasAsync(
        Solicitacao solicitacao, ExameImagem? principal, CancellationToken ct)
    {
        // Sem principal vivo não há accession base nem pedido em execução.
        if (principal is null || principal.ExcluidoEm is not null) return ResultadoSatelite.NadaAFazer;

        var itens = await db.SolicitacaoProcedimentos
            .Where(p => p.SolicitacaoId == solicitacao.Id && p.ExcluidoEm == null && p.Ordem >= 2)
            .OrderBy(p => p.Ordem)
            .ToListAsync(ct);
        if (itens.Count == 0) return ResultadoSatelite.NadaAFazer;

        var existentes = await db.ExamesImagem
            .Where(e => e.SolicitacaoId == solicitacao.Id && e.Ordem > 0)
            .ToListAsync(ct);

        var resultado = ResultadoSatelite.NadaAFazer;
        var agora = DateTime.UtcNow;
        foreach (var item in itens)
        {
            var exame = existentes.FirstOrDefault(e => e.SolicitacaoProcedimentoId == item.Id);
            if (exame is not null && (exame.TipoExameId is not null || exame.ExcluidoEm is not null))
                continue;

            var (criaSatelite, tipoExameId) = await resolvedorTipoExame.ResolverParaExecucaoAsync(
                solicitacao.Categoria,
                ResolvedorTipoExameSisreg.NormalizarNome(item.ProcedimentoTexto ?? string.Empty),
                SoDigitos(item.ProcedimentoCodigoSisreg), SoDigitos(item.ProcedimentoSigtapCodigo),
                solicitacao.UnidadeExecutanteId, ct);
            // Fora da imagem, procedimento extra sem tipo com modalidade e worklist na unidade não
            // vira exame.
            if (exame is null && !criaSatelite) continue;
            if (tipoExameId is { } tipo)
                await escopoExameUnidade.GarantirAsync(tipo, solicitacao.UnidadeExecutanteId, ct);

            if (exame is not null)
            {
                if (tipoExameId is null) continue;
                exame.TipoExameId = tipoExameId;
                exame.AtualizadoEm = agora;
                exame.AtualizadoPor = usuarioAtual.UsuarioId;
                if (resultado == ResultadoSatelite.NadaAFazer) resultado = ResultadoSatelite.TipoCompletado;
                continue;
            }

            // A posição segue a do item (item 2 → exame 1 → "-1"). Se ela já estiver ocupada (exame
            // de um item que foi excluído), vai para a próxima livre: accession emitido não se reusa.
            var ordem = item.Ordem - 1;
            if (existentes.Any(e => e.Ordem == ordem))
                ordem = existentes.Max(e => e.Ordem) + 1;

            exame = new ExameImagem
            {
                Id = Guid.CreateVersion7(),
                SolicitacaoId = solicitacao.Id,
                SolicitacaoPrincipalId = null,
                Ordem = ordem,
                SolicitacaoProcedimentoId = item.Id,
                AccessionNumber = Solicitacoes.ExamesDoPedido.Accession(principal.AccessionNumber, ordem),
                StudyInstanceUID = geradorIds.NovoStudyInstanceUid(),
                TipoExameId = tipoExameId,
                Status = StatusSolicitacaoExame.Solicitada,
                ProximaTentativaEm = null,
                CriadoEm = agora,
                CriadoPor = usuarioAtual.UsuarioId,
            };

            if (solicitacao.AutorizadoEm is not null && principal.Status != StatusSolicitacaoExame.Cancelada)
            {
                exame.ProximaTentativaEm = agora;
                exame.EquipamentoId = await MesmoEquipamentoDoPrincipalAsync(principal, tipoExameId, ct);
            }

            db.ExamesImagem.Add(exame);
            existentes.Add(exame);
            resultado = ResultadoSatelite.Criado;
        }
        return resultado;
    }

    /// <summary>A sala que a recepção escolheu para o principal, quando ela também faz a
    /// modalidade do extra; senão null, e o envio deduz a estação como faz para qualquer exame.</summary>
    private async Task<Guid?> MesmoEquipamentoDoPrincipalAsync(
        ExameImagem principal, Guid? tipoExameId, CancellationToken ct)
    {
        if (principal.EquipamentoId is not { } equipamentoId || tipoExameId is not { } tipo) return null;
        var modalidadeTipo = await db.TiposExame.AsNoTracking()
            .Where(t => t.Id == tipo).Select(t => (ModalidadeDicom?)t.ModalidadeDicom).FirstOrDefaultAsync(ct);
        var modalidadeEquip = await db.Equipamentos.AsNoTracking()
            .Where(e => e.Id == equipamentoId).Select(e => (ModalidadeDicom?)e.ModalidadeDicom).FirstOrDefaultAsync(ct);
        return modalidadeTipo is not null && modalidadeTipo == modalidadeEquip ? equipamentoId : null;
    }

    public async Task<ReparoSatelitesResultado> RepararAsync(
        int limite, Guid? depoisDe, bool incluirAnterioresForaDaImagem, CancellationToken ct)
    {
        limite = Math.Clamp(limite, 1, 2000);

        // Início do dia de Brasília em UTC: os agendados de hoje ainda podem chegar à recepção.
        var hojeBrasilia = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3)).Date;
        var desdeUtc = DateTime.SpecifyKind(hojeBrasilia.AddHours(3), DateTimeKind.Utc);

        // Fora da imagem, só o procedimento cujo tipo tem modalidade DICOM E está no escopo da
        // executante com o envio à worklist ligado ganha satélite (CategoriaSigtap.CriaSatelite).
        // ProcedimentoTexto e TipoExame.Nome usam a mesma normalização (NomeTipoExame.Normalizar),
        // então a comparação é direta.
        var escoposComWorklist = db.TiposExameUnidade.Where(a =>
            a.Ativo && a.EnviarParaWorklist && a.ExcluidoEm == null
            && a.TipoExame!.ExcluidoEm == null && a.TipoExame.ModalidadeDicom != ModalidadeDicom.Indefinida);

        IQueryable<Solicitacao> Pendentes() => db.Solicitacoes.Where(s =>
            s.ExcluidoEm == null
            && s.Status != StatusSolicitacao.Cancelada
            && (s.Categoria == CategoriaSolicitacao.Imagem
                // Imagem: só de hoje em diante — a carga de 08/09 deixou ~512 mil antigas sem
                // satélite, e não é o reparo que vai ressuscitá-las.
                ? s.DataAgendada >= desdeUtc
                  && (!db.ExamesImagem.Any(e => e.SolicitacaoId == s.Id)
                      || db.ExamesImagem.Any(e => e.SolicitacaoId == s.Id && e.ExcluidoEm == null && e.TipoExameId == null))
                // Holter, MAPA…: o retroativo é pedido explícito (acervo de laudos em PDF a trazer).
                : (incluirAnterioresForaDaImagem || s.DataAgendada >= desdeUtc)
                  && s.ProcedimentoTexto != null
                  && escoposComWorklist.Any(a =>
                      a.UnidadeId == s.UnidadeExecutanteId && a.TipoExame!.Nome == s.ProcedimentoTexto)
                  && !db.ExamesImagem.Any(e => e.SolicitacaoId == s.Id)));

        // Cursor por id (não por data): o exame sem nome de procedimento fica sem tipo e continua
        // "pendente" para sempre — ordenado por data, travaria o lote no mesmo começo a cada chamada.
        var ids = await Pendentes()
            .Where(s => depoisDe == null || s.Id.CompareTo(depoisDe.Value) > 0)
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .Take(limite)
            .ToListAsync(ct);

        // Accession em sequência local: o gerador relê TODOS os accessions do dia a cada chamada,
        // e milhares de chamadas seguidas viram milhões de linhas trafegadas. Relê só se colidir
        // com um exame criado pela recepção no meio do lote.
        var proxima = await geradorIds.ProximoAccessionAsync(ct);
        string Consumir()
        {
            var atual = proxima;
            var prefixo = atual[..6];
            proxima = $"{prefixo}{int.Parse(atual[6..]) + 1:D3}";
            return atual;
        }

        int criados = 0, tipos = 0;
        foreach (var id in ids)
        {
            for (var tentativa = 1; ; tentativa++)
            {
                var solic = await db.Solicitacoes.FirstAsync(s => s.Id == id, ct);
                var resultado = await GarantirAsync(solic, Consumir, ct);
                try
                {
                    // Um a um: o tipo e o par de escopo criados aqui precisam estar no banco antes
                    // da próxima solicitação com o mesmo procedimento consultá-los.
                    if (resultado is ResultadoSatelite.Criado or ResultadoSatelite.TipoCompletado)
                        await db.SaveChangesAsync(ct);
                }
                catch (DbUpdateException) when (tentativa == 1)
                {
                    db.ChangeTracker.Clear();
                    proxima = await geradorIds.ProximoAccessionAsync(ct);
                    continue;
                }

                if (resultado == ResultadoSatelite.Criado) criados++;
                else if (resultado == ResultadoSatelite.TipoCompletado) tipos++;
                break;
            }
            db.ChangeTracker.Clear();
        }

        Guid? cursor = ids.Count > 0 ? ids[^1] : depoisDe;
        var restantes = await Pendentes()
            .Where(s => cursor == null || s.Id.CompareTo(cursor.Value) > 0)
            .CountAsync(ct);
        return new ReparoSatelitesResultado(criados, tipos, cursor, restantes);
    }

    private static string SoDigitos(string? s) =>
        string.IsNullOrEmpty(s) ? string.Empty : new string([.. s.Where(char.IsDigit)]);
}

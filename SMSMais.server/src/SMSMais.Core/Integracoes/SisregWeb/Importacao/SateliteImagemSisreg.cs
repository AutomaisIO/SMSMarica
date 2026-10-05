using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Identidade;
using SMSMais.Core.SolicitacoesExame;
using SMSMais.Core.SolicitacoesExame.Identificadores;
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
/// <para>Espelha o caminho de criação da importação: tipo pelo NOME do SISREG
/// (<see cref="IResolvedorTipoExameSisreg"/>, nasce com worklist desligado), par tipo×unidade no
/// escopo e status <c>Solicitada</c> sem próxima tentativa — nada vai ao PACS até a recepção
/// autorizar.</para>
/// </summary>
public interface ISateliteImagemSisreg
{
    /// <summary>
    /// Cria o satélite que falta, ou completa o tipo de um satélite que nasceu sem ele. NÃO salva.
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
    Task<ReparoSatelitesResultado> RepararAsync(int limite, Guid? depoisDe, CancellationToken ct);
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
        if (solicitacao.Categoria != CategoriaSolicitacao.Imagem
            || solicitacao.Status == StatusSolicitacao.Cancelada
            || solicitacao.ExcluidoEm is not null)
            return ResultadoSatelite.NadaAFazer;

        // Sem filtro de ExcluidoEm: o índice único de solicitacao_id não é filtrado — um satélite
        // excluído ainda ocupa a vaga, e criar outro estouraria.
        var exame = await db.ExamesImagem.FirstOrDefaultAsync(e => e.SolicitacaoId == solicitacao.Id, ct);
        if (exame is not null && (exame.TipoExameId is not null || exame.ExcluidoEm is not null))
            return ResultadoSatelite.NadaAFazer;

        var nome = ResolvedorTipoExameSisreg.NormalizarNome(solicitacao.ProcedimentoTexto ?? string.Empty);
        var sig = SoDigitos(solicitacao.ProcedimentoSigtapCodigo);
        var tipoExameId = await resolvedorTipoExame.ResolverOuCriarAsync(
            nome, SoDigitos(solicitacao.ProcedimentoCodigoSisreg), sig, ct);
        if (tipoExameId is { } tipo)
            await escopoExameUnidade.GarantirAsync(tipo, solicitacao.UnidadeExecutanteId, ct);

        var agora = DateTime.UtcNow;
        if (exame is not null)
        {
            if (tipoExameId is null) return ResultadoSatelite.NadaAFazer;
            exame.TipoExameId = tipoExameId;
            exame.AtualizadoEm = agora;
            exame.AtualizadoPor = usuarioAtual.UsuarioId;
            return ResultadoSatelite.TipoCompletado;
        }

        db.ExamesImagem.Add(new ExameImagem
        {
            Id = Guid.CreateVersion7(),
            SolicitacaoId = solicitacao.Id,
            AccessionNumber = proximoAccession?.Invoke() ?? await geradorIds.ProximoAccessionAsync(ct),
            StudyInstanceUID = geradorIds.NovoStudyInstanceUid(),
            TipoExameId = tipoExameId, // nullable = mapeamento pendente
            Status = StatusSolicitacaoExame.Solicitada,
            // NADA vai ao PACS automaticamente: só quando a recepção AUTORIZA.
            ProximaTentativaEm = null,
            CriadoEm = agora,
            CriadoPor = usuarioAtual.UsuarioId,
        });
        return ResultadoSatelite.Criado;
    }

    public async Task<ReparoSatelitesResultado> RepararAsync(int limite, Guid? depoisDe, CancellationToken ct)
    {
        limite = Math.Clamp(limite, 1, 2000);

        // Início do dia de Brasília em UTC: os agendados de hoje ainda podem chegar à recepção.
        var hojeBrasilia = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3)).Date;
        var desdeUtc = DateTime.SpecifyKind(hojeBrasilia.AddHours(3), DateTimeKind.Utc);

        IQueryable<Solicitacao> Pendentes() => db.Solicitacoes.Where(s =>
            s.ExcluidoEm == null
            && s.Categoria == CategoriaSolicitacao.Imagem
            && s.Status != StatusSolicitacao.Cancelada
            && s.DataAgendada >= desdeUtc
            && (!db.ExamesImagem.Any(e => e.SolicitacaoId == s.Id)
                || db.ExamesImagem.Any(e => e.SolicitacaoId == s.Id && e.ExcluidoEm == null && e.TipoExameId == null)));

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

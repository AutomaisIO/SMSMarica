using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Worklist;

/// <summary>
/// Resolve o AE Title da estação pelo <see cref="Equipamento"/> cadastrado — casa a
/// unidade executante da solicitação com a modalidade do tipo de exame.
///
/// Sem equipamento cadastrado, o envio FALHA com "Sem equipamento configurado" em vez
/// de carimbar um AE genérico: mandar o exame de uma unidade para a estação de outra é
/// pior que não mandar. O erro aparece no exame (<c>ErroIntegracaoPacs</c>) e o worker
/// retenta com backoff — cadastrar o equipamento resolve sozinho, sem reprocessar nada.
/// </summary>
public sealed class ResolvedorEstacaoWorklist(
    SmsMaricaDbContext db,
    ILogger<ResolvedorEstacaoWorklist> logger) : IResolvedorEstacaoWorklist
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly ILogger<ResolvedorEstacaoWorklist> _logger = logger;

    public async Task<string> ResolverAsync(ExameImagem exame, CancellationToken cancellationToken = default)
    {
        var modalidade = await ResolverModalidadeAsync(exame, cancellationToken)
            ?? throw new ConflitoException("worklist.sem_modalidade",
                "Sem tipo de exame definido — não dá para saber a modalidade nem o equipamento de destino.");

        var unidadeId = await ResolverUnidadeExecutanteAsync(exame, cancellationToken)
            ?? throw new ConflitoException("worklist.sem_unidade",
                "Solicitação sem unidade executante — não dá para determinar o equipamento de destino.");

        // Ordem por nome deixa a escolha estável quando a unidade tem mais de um
        // equipamento da mesma modalidade (ex.: duas salas de ultrassom).
        var candidatos = await _db.Equipamentos.AsNoTracking()
            .Where(e => e.UnidadeId == unidadeId
                        && e.ModalidadeDicom == modalidade
                        && e.Ativo
                        && e.ExcluidoEm == null
                        && e.IdentificadorDicom != null)
            .OrderBy(e => e.Nome)
            .Select(e => new { e.Nome, e.IdentificadorDicom })
            .ToListAsync(cancellationToken);

        var validos = candidatos.Where(c => AeTitleValido(c.IdentificadorDicom)).ToList();

        foreach (var invalido in candidatos.Except(validos))
        {
            _logger.LogWarning(
                "Equipamento '{Equipamento}' tem identificador DICOM inválido para AE Title ('{Ae}') — ignorado na worklist.",
                invalido.Nome, invalido.IdentificadorDicom);
        }

        if (validos.Count == 0)
        {
            var unidade = await _db.Unidades.AsNoTracking()
                .Where(u => u.Id == unidadeId)
                .Select(u => u.Nome)
                .FirstOrDefaultAsync(cancellationToken) ?? unidadeId.ToString();

            throw new ConflitoException("worklist.sem_equipamento",
                $"Sem equipamento configurado: a unidade {unidade} não tem equipamento {modalidade} ativo com AE Title. " +
                "Cadastre em Exames de Imagem → Equipamentos.");
        }

        if (validos.Count > 1)
        {
            _logger.LogWarning(
                "Unidade {Unidade} tem {N} equipamentos {Modalidade} com AE Title — usando '{Ae}' para {Accession}.",
                unidadeId, validos.Count, modalidade, validos[0].IdentificadorDicom, exame.AccessionNumber);
        }

        return validos[0].IdentificadorDicom!.Trim();
    }

    private async Task<ModalidadeDicom?> ResolverModalidadeAsync(ExameImagem exame, CancellationToken ct)
    {
        if (exame.TipoExame is not null) return exame.TipoExame.ModalidadeDicom;
        if (exame.TipoExameId is not { } tipoExameId) return null;

        return await _db.TiposExame.AsNoTracking()
            .Where(t => t.Id == tipoExameId)
            .Select(t => (ModalidadeDicom?)t.ModalidadeDicom)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<Guid?> ResolverUnidadeExecutanteAsync(ExameImagem exame, CancellationToken ct)
    {
        if (exame.Solicitacao is not null) return exame.Solicitacao.UnidadeExecutanteId;

        return await _db.Solicitacoes.AsNoTracking()
            .Where(s => s.Id == exame.SolicitacaoId)
            .Select(s => (Guid?)s.UnidadeExecutanteId)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>AE Title (VR "AE"): até 16 caracteres, ASCII imprimível, sem barra
    /// invertida e sem espaço (o espaço só é permitido como padding — equipamento
    /// nenhum lida bem com ele no meio).</summary>
    internal static bool AeTitleValido(string? valor)
    {
        var ae = valor?.Trim();
        if (string.IsNullOrEmpty(ae) || ae.Length > 16) return false;
        return ae.All(c => c is > (char)0x20 and < (char)0x7F && c != '\\');
    }
}

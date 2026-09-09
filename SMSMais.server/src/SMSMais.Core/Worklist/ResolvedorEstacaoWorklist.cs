using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Worklist;

/// <summary>
/// Resolve o AE Title da estação pelo <see cref="Equipamento"/>: o escolhido no exame, ou o
/// único da unidade executante naquela modalidade.
///
/// Nunca inventa destino. Sem equipamento, o envio falha com "Sem equipamento configurado";
/// com mais de um e sem escolha, falha pedindo a seleção. Mandar o exame de uma unidade para a
/// estação de outra — ou sortear entre duas salas — é pior que não mandar.
/// </summary>
public sealed class ResolvedorEstacaoWorklist(
    SmsMaisDbContext db,
    IEscopoExameUnidade escopoExame,
    ILogger<ResolvedorEstacaoWorklist> logger) : IResolvedorEstacaoWorklist
{
    private readonly SmsMaisDbContext _db = db;
    private readonly IEscopoExameUnidade _escopoExame = escopoExame;
    private readonly ILogger<ResolvedorEstacaoWorklist> _logger = logger;

    public async Task<EstacaoWorklist> ResolverAsync(ExameImagem exame, CancellationToken cancellationToken = default)
    {
        // Escolha explícita da recepção manda — inclusive se a unidade ganhou outro equipamento depois.
        if (exame.EquipamentoId is { } escolhido)
        {
            var eq = await _db.Equipamentos.AsNoTracking()
                .Where(e => e.Id == escolhido && e.Ativo && e.ExcluidoEm == null)
                .Select(e => new { e.Nome, e.IdentificadorDicom, e.DescricaoMaxCaracteres })
                .FirstOrDefaultAsync(cancellationToken);

            if (eq is not null && AeTitleValido(eq.IdentificadorDicom))
                return new EstacaoWorklist(eq.IdentificadorDicom!.Trim(), eq.DescricaoMaxCaracteres);

            // Equipamento escolhido saiu do ar (desativado/excluído/AE apagado): cai na dedução,
            // que ou acha um substituto único ou para e pede escolha nova.
            _logger.LogWarning(
                "Equipamento {Id} escolhido para {Accession} não está mais utilizável — rededuzindo.",
                escolhido, exame.AccessionNumber);
        }

        // Destino configurado no escopo da unidade: a resposta fixa, quando existe. Vem antes da
        // dedução de propósito — é o que tira o casamento por modalidade do caminho crítico. Foi
        // esse casamento que, em 04/09/2026, fez cinco radiografias marcadas como MG apontarem
        // para o mamógrafo do CDT.
        var escopo = await _escopoExame.ObterAsync(exame, cancellationToken);
        if (escopo?.Equipamento is { } configurado
            && configurado.Ativo && configurado.ExcluidoEm == null
            && AeTitleValido(configurado.IdentificadorDicom))
        {
            return new EstacaoWorklist(
                configurado.IdentificadorDicom!.Trim(), configurado.DescricaoMaxCaracteres);
        }

        var candidatos = await ListarCandidatosAsync(exame, cancellationToken);

        if (candidatos.Count == 0)
        {
            var unidade = await NomeUnidadeAsync(exame, cancellationToken);
            var modalidade = await ResolverModalidadeAsync(exame, cancellationToken);
            throw new ConflitoException("worklist.sem_equipamento",
                $"Sem equipamento configurado: a unidade {unidade} não tem equipamento {modalidade} ativo com AE Title. " +
                "Cadastre em Exames de Imagem → Equipamentos.");
        }

        if (candidatos.Count > 1)
        {
            var nomes = string.Join(", ", candidatos.Select(c => c.Nome));
            throw new ConflitoException("worklist.equipamento_ambiguo",
                $"A unidade tem mais de um equipamento para esta modalidade ({nomes}). " +
                "Selecione em qual o exame será realizado.");
        }

        return new EstacaoWorklist(candidatos[0].AeTitle, candidatos[0].DescricaoMaxCaracteres);
    }

    public async Task<IReadOnlyList<EquipamentoCandidato>> ListarCandidatosAsync(
        ExameImagem exame, CancellationToken cancellationToken = default)
    {
        var modalidade = await ResolverModalidadeAsync(exame, cancellationToken);
        var unidadeId = await ResolverUnidadeExecutanteAsync(exame, cancellationToken);
        if (modalidade is null || unidadeId is null) return [];

        var equipamentos = await _db.Equipamentos.AsNoTracking()
            .Where(e => e.UnidadeId == unidadeId
                        && e.ModalidadeDicom == modalidade
                        && e.Ativo
                        && e.ExcluidoEm == null
                        && e.IdentificadorDicom != null)
            .OrderBy(e => e.Nome)
            .Select(e => new { e.Id, e.Nome, e.IdentificadorDicom, e.DescricaoMaxCaracteres })
            .ToListAsync(cancellationToken);

        var validos = new List<EquipamentoCandidato>(equipamentos.Count);
        foreach (var e in equipamentos)
        {
            if (AeTitleValido(e.IdentificadorDicom))
                validos.Add(new EquipamentoCandidato(
                    e.Id, e.Nome, e.IdentificadorDicom!.Trim(), e.DescricaoMaxCaracteres));
            else
                _logger.LogWarning(
                    "Equipamento '{Equipamento}' tem identificador DICOM inválido para AE Title ('{Ae}') — fora da worklist.",
                    e.Nome, e.IdentificadorDicom);
        }
        return validos;
    }

    private async Task<string> NomeUnidadeAsync(ExameImagem exame, CancellationToken ct)
    {
        var unidadeId = await ResolverUnidadeExecutanteAsync(exame, ct);
        if (unidadeId is null) return "(sem unidade executante)";
        return await _db.Unidades.AsNoTracking()
            .Where(u => u.Id == unidadeId)
            .Select(u => u.Nome)
            .FirstOrDefaultAsync(ct) ?? unidadeId.ToString()!;
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

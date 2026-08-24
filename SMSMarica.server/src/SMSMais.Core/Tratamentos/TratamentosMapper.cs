using SMSMais.Core.Tratamentos.Dtos;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Tratamentos;

internal static class TratamentosMapper
{
    public sealed record AlocacaoAtiva(
        Guid SessaoId,
        Guid RotaId,
        DateOnly Data,
        int FileiraOrdem,
        int NumeroAssento);

    public static SessaoDto ParaSessaoDto(SessaoDeTratamento s, AlocacaoAtiva? alocacao) => new(
        s.Id, s.TratamentoId, s.DataPrevista,
        s.HoraPrevistaBusca, s.HoraPrevistaRetorno, s.Status,
        s.RealizadaEm, s.NomeAcompanhante, s.ParentescoAcompanhante,
        s.MotoristaIdaId, s.VeiculoIdaId, s.HoraSaidaResidencia, s.HoraChegadaUnidade,
        s.MotoristaVoltaId, s.VeiculoVoltaId, s.HoraSaidaUnidade, s.HoraChegadaResidencia,
        s.MotivoNaoRealizacao, s.Observacoes,
        alocacao?.RotaId,
        alocacao?.Data,
        alocacao?.FileiraOrdem,
        alocacao?.NumeroAssento);

    public static TratamentoDto ParaDto(
        Tratamento t,
        IReadOnlyDictionary<Guid, AlocacaoAtiva> alocacoesPorSessao) => new(
        t.Id,
        t.PacienteId,
        // Nome do paciente resolve via hub FHIR (GET /pacientes/{PacienteId}). TODO embutir.
        string.Empty,
        t.UnidadeId,
        t.Unidade?.Nome ?? string.Empty,
        t.TipoTratamentoId,
        t.TipoTratamento?.Nome,
        t.Descricao,
        t.CodigoSusLiberacao,
        t.Observacoes,
        t.HoraPrevistaBusca,
        t.Ativo,
        t.CriadoEm,
        t.EncerradoEm,
        t.Periodicidade is null ? null : new PeriodicidadeDto(
            t.Periodicidade.Id,
            t.Periodicidade.Tipo,
            t.Periodicidade.IntervaloDias,
            t.Periodicidade.DiasSemanaMascara,
            t.Periodicidade.DataInicio,
            t.Periodicidade.QuantidadeSessoes),
        [.. t.Sessoes.OrderBy(s => s.DataPrevista).Select(s =>
            ParaSessaoDto(s, alocacoesPorSessao.GetValueOrDefault(s.Id)))]);

    public static TipoTratamentoDto ParaTipoDto(TipoTratamento t) =>
        new(t.Id, t.Nome, t.Codigo, t.Ativo);
}

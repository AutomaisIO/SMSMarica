using SMSMarica.Core.Tratamentos.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Tratamentos;

internal static class TratamentosMapper
{
    public static SessaoDto ParaSessaoDto(SessaoDeTratamento s) => new(
        s.Id, s.TratamentoId, s.DataPrevista,
        s.HoraPrevistaBusca, s.HoraPrevistaRetorno, s.Status,
        s.RealizadaEm, s.NomeAcompanhante, s.ParentescoAcompanhante,
        s.MotoristaIdaId, s.VeiculoIdaId, s.HoraSaidaResidencia, s.HoraChegadaUnidade,
        s.MotoristaVoltaId, s.VeiculoVoltaId, s.HoraSaidaUnidade, s.HoraChegadaResidencia,
        s.MotivoNaoRealizacao, s.Observacoes);

    public static TratamentoDto ParaDto(Tratamento t) => new(
        t.Id,
        t.PacienteId,
        t.Paciente?.NomeCompleto ?? string.Empty,
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
        [.. t.Sessoes.OrderBy(s => s.DataPrevista).Select(ParaSessaoDto)]);

    public static TipoTratamentoDto ParaTipoDto(TipoTratamento t) =>
        new(t.Id, t.Nome, t.Codigo, t.Ativo);
}

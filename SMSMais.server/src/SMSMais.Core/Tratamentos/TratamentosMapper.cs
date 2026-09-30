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
        [.. s.Acompanhantes
            .Where(x => x.Acompanhante is not null)
            .OrderBy(x => x.Acompanhante!.Nome)
            .Select(x => new AcompanhanteDaSessaoDto(x.AcompanhanteId, x.Acompanhante!.Nome, x.Acompanhante.Parentesco))],
        s.MotoristaIdaId, s.VeiculoIdaId, s.HoraSaidaResidencia, s.HoraChegadaUnidade,
        s.MotoristaVoltaId, s.VeiculoVoltaId, s.HoraSaidaUnidade, s.HoraChegadaResidencia,
        s.MotivoNaoRealizacao, s.Observacoes,
        alocacao?.RotaId,
        alocacao?.Data,
        alocacao?.FileiraOrdem,
        alocacao?.NumeroAssento);

    public static AgendaDto ParaAgendaDto(Tratamento t) =>
        new(t.DataInicio, t.DiasSemanaMascara, t.QuantidadeSessoes, t.Continuo, t.SessoesGeradasAte);

    public static NecessidadesDto ParaNecessidadesDto(Tratamento t) =>
        new(t.Mobilidade, t.DificuldadeVeiculoAlto, t.Isolamento, t.UsaOxigenio, t.NecessitaAjuda, t.AjudaDescricao);

    public static TratamentoDto ParaDto(
        Tratamento t,
        IReadOnlyDictionary<Guid, AlocacaoAtiva> alocacoesPorSessao,
        string? liberadoPorNome) => new(
        t.Id,
        t.PacienteId,
        // Nome do paciente resolve via hub FHIR; o service embute depois.
        string.Empty,
        t.UnidadeAtendimentoId,
        t.UnidadeAtendimento?.Nome ?? string.Empty,
        t.UnidadeAtendimento?.Endereco?.Cidade,
        t.TipoTratamentoId,
        t.TipoTratamento?.Nome,
        t.TipoTratamento?.TempoMedioMinutos,
        t.Descricao,
        t.Observacoes,
        ParaAgendaDto(t),
        ParaNecessidadesDto(t),
        new RegraAcompanhantesDto(
            t.QuantidadeAcompanhantes,
            t.SegundoAcompanhanteJustificativa,
            liberadoPorNome,
            t.SegundoAcompanhanteLiberadoEm),
        t.Ativo,
        t.CriadoEm,
        t.EncerradoEm,
        [.. t.Sessoes.OrderBy(s => s.DataPrevista).Select(s =>
            ParaSessaoDto(s, alocacoesPorSessao.GetValueOrDefault(s.Id)))]);

    public static TipoTratamentoDto ParaTipoDto(TipoTratamento t) =>
        new(t.Id, t.Nome, t.Codigo, t.TempoMedioMinutos, t.Ativo);
}

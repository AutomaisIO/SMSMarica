using Riok.Mapperly.Abstractions;
using SMSMarica.Core.Tratamentos.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Tratamentos;

[Mapper]
internal static partial class TratamentosMapper
{
    public static TratamentoDto ParaDto(Tratamento t) => new(
        t.Id, t.PacienteId, t.UnidadeId, t.Descricao,
        t.Ativo, t.CriadoEm, t.EncerradoEm,
        t.Periodicidade is null ? null : new PeriodicidadeDto(
            t.Periodicidade.Id,
            t.Periodicidade.Tipo,
            t.Periodicidade.IntervaloDias,
            t.Periodicidade.DiasSemanaMascara,
            t.Periodicidade.DataInicio,
            t.Periodicidade.QuantidadeSessoes));

    public static TratamentoListItemDto ParaListItem(Tratamento t) =>
        new(t.Id, t.PacienteId, t.UnidadeId, t.Descricao, t.Ativo);
}

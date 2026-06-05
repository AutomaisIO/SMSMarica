namespace SMSMarica.Core.Integracoes.Sisreg.Dtos;

/// <summary>Intervalo fechado de datas [Inicio, Fim] usado nos filtros <c>range</c> das consultas SISREG.</summary>
public sealed record IntervaloDatas(DateOnly Inicio, DateOnly Fim);

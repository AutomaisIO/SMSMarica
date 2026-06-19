using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities.Tfd;

/// <summary>
/// Registro de faturamento de um transporte (uma sessão realizada). A quantidade é
/// <b>proporcional</b>: <c>Unidades = KmComPaciente / KmPorUnidade</c> (ex.: 170 km / 50 =
/// 3,4). O valor é <c>Unidades × ValorUnitario</c>. Dimensões (motorista, veículo, tipo de
/// tratamento, unidade) ficam em snapshot para os relatórios. Ver FT10 / faturamento.md.
/// </summary>
public class RegistroFaturamento
{
    public Guid Id { get; set; }
    public Guid SessaoId { get; set; }

    // --- Dimensões (snapshot p/ relatórios) ---
    public Guid PacienteId { get; set; }        // hub FHIR
    public Guid? MotoristaId { get; set; }
    public Guid? VeiculoId { get; set; }
    public Guid? TipoTratamentoId { get; set; }
    public Guid UnidadeId { get; set; }

    public int Competencia { get; set; }         // AAAAMM
    public DateOnly Data { get; set; }

    // --- Cálculo ---
    public decimal KmComPaciente { get; set; }
    public decimal Unidades { get; set; }        // proporcional (Km / KmPorUnidade)
    public decimal ValorUnitario { get; set; }   // snapshot do valor por unidade
    public decimal ValorTotal { get; set; }      // Unidades × ValorUnitario
    public string? CodigoSigtap { get; set; }    // snapshot do código configurado

    public StatusFaturamento Status { get; set; } = StatusFaturamento.Pendente;
    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    public SessaoDeTratamento? Sessao { get; set; }
}

namespace SMSMais.Data.Entities;

/// <summary>
/// Unidade de atendimento do Transporte de Pacientes: o DESTINO da van — onde o paciente faz o
/// tratamento (clínica de hemodiálise, hospital de referência, centro de oncologia...).
/// Cadastro manual e próprio do transporte; não é a <see cref="Unidade"/> de saúde, que vem do
/// SISREG/CNES e carrega escopo de usuário, equipamentos e integrações. Aqui o que importa é o
/// endereço e a coordenada: <see cref="Gps"/> é o ponto final da rota calculada.
/// </summary>
public class UnidadeAtendimento
{
    public Guid Id { get; set; }
    /// <summary>Sempre em MAIÚSCULAS (normalizado pelo serviço).</summary>
    public string Nome { get; set; } = string.Empty;
    public Endereco? Endereco { get; set; }

    /// <summary>Ponto de chegada da van. Obrigatório pela regra do serviço (sem ele não há rota);
    /// nulo só em linha legada.</summary>
    public Gps? Gps { get; set; }

    public string? Telefone { get; set; }

    /// <summary>Orientação de chegada para o motorista (portão, acesso de ambulância, horário).</summary>
    public string? Observacoes { get; set; }

    /// <summary>Fora do município (TFD). Marcado no cadastro; o rastreamento só lista "aguardando
    /// retorno" nas unidades externas.</summary>
    public bool Externa { get; set; }

    public bool Ativo { get; set; } = true;
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}

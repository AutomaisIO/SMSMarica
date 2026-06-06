using SMSMarica.Core.Integracoes.Pep.Fhir;
using SMSMarica.Core.Integracoes.Pep.Progresso;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Integracoes.Pep.Estrategias;

/// <summary>
/// Estratégia de importação de um tipo de PEP (Salux hoje; MV/Eco no futuro). Cada
/// estratégia conhece o SQL da sua origem e o mapeamento para FHIR R4. O orquestrador
/// resolve a estratégia por <see cref="Tipo"/> a partir da <c>IaFonte</c> selecionada.
/// </summary>
public interface IEstrategiaImportacaoPep
{
    TipoFonte Tipo { get; }

    Task ImportarAsync(ContextoImportacaoPep contexto, CancellationToken ct);
}

/// <summary>Dados de conexão de uma base (já decifrados — nunca logar a senha).</summary>
public sealed record ConexaoFonte(
    string Host,
    int Porta,
    string Servico,
    string Usuario,
    string Senha,
    int TimeoutSegundos);

/// <summary>Parâmetros do disparo de importação.</summary>
public sealed record OpcoesImportacao(
    ModoSincronizacao Modo,
    EscopoSincronizacao Escopo,
    int? MaxMedicos,
    int? MaxPacientes,
    bool ApagarAntes);

/// <summary>
/// Marca d'água por entidade (in/out). No modo incremental a estratégia usa os valores de
/// entrada como filtro <c>since</c> e atualiza para o máximo importado; o orquestrador
/// persiste só ao concluir com sucesso.
/// </summary>
public sealed class MarcaDagua
{
    public DateTime? MedicoEm { get; set; }
    public DateTime? PacienteEm { get; set; }
    public DateTime? BaaEm { get; set; }
    public DateTime? EdocEm { get; set; }
}

/// <summary>Tudo que a estratégia precisa para rodar um run, mais o canal de progresso.</summary>
public sealed class ContextoImportacaoPep
{
    public required ConexaoFonte Conexao { get; init; }
    public required OpcoesImportacao Opcoes { get; init; }
    public required MarcaDagua Marca { get; init; }
    public required IHubFhirEscritor Escritor { get; init; }
    public required ProgressoImportacao Progresso { get; init; }
}

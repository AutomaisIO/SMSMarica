using SMSMarica.Core.Integracoes.Pep.Falhas;
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

/// <summary>
/// Parâmetros do disparo de importação. <see cref="CdsPacientes"/> (opcional) força importar
/// exatamente esses pacientes (reimport pontual / medição), ignorando o limite/recência.
/// </summary>
public sealed record OpcoesImportacao(
    ModoSincronizacao Modo,
    EscopoSincronizacao Escopo,
    int? MaxMedicos,
    int? MaxPacientes,
    bool ApagarAntes,
    IReadOnlyList<long>? CdsPacientes = null,
    int? Concorrencia = null,
    /// <summary>
    /// Ponteiro inicial de <c>cd_paciente</c> no modo COMPLETO/escopo Tudo: começa
    /// a paginação a partir dele (retoma de onde parou ou de um ponto manual).
    /// Null = começa do topo. Ignorado nos demais modos/escopos.
    /// </summary>
    long? CursorPacienteInicial = null);

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

    /// <summary>Slug curto e estável da base (IaFonte) — prefixa identifiers internos e compõe o meta.source.</summary>
    public required string BaseSlug { get; init; }

    /// <summary>
    /// Sink durável de falhas (opcional). Quando presente, cada falha registrada na importação
    /// é persistida na hora em <c>smsmarica.pep_sincronizacao_falha</c> — trilha que sobrevive a
    /// crash e alimenta o reimport direcionado por cd. Null em cenários sem persistência (testes).
    /// </summary>
    public IRegistradorFalhasPep? Falhas { get; init; }

    /// <summary>
    /// Callback para persistir o cursor de retomada (cd_paciente do último bloco
    /// concluído) a cada bloco do modo COMPLETO; recebe <c>null</c> ao terminar a
    /// base inteira. Null em cenários sem persistência (testes).
    /// </summary>
    public Func<long?, CancellationToken, Task>? SalvarCursorPaciente { get; init; }
}

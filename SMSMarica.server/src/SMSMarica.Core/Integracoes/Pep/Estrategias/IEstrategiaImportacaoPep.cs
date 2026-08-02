using SMSMarica.Core.Integracoes.Pep.Divergencias;
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
    long? CursorPacienteInicial = null,
    /// <summary>
    /// Força o re-scan integral de médicos mesmo com a marca preenchida. O scheduler liga
    /// isto quando a marca de médicos envelhece além de <c>MedicoRescanHoras</c> da agenda.
    /// </summary>
    bool ForcarMedicos = false);

/// <summary>
/// Marca d'água por entidade (in/out). No modo incremental a estratégia usa os valores de
/// entrada como filtro <c>since</c> e atualiza para o MÁXIMO DO TIMESTAMP DA ORIGEM
/// processado (nunca "agora"). A estratégia persiste ao fim de cada fase concluída via
/// <see cref="ContextoImportacaoPep.SalvarMarca"/> — um run que morre no meio preserva o
/// avanço das fases anteriores (ADR-0024); o orquestrador persiste de novo no sucesso.
/// </summary>
public sealed class MarcaDagua
{
    public DateTime? MedicoEm { get; set; }
    public DateTime? PacienteEm { get; set; }
    public DateTime? BaaEm { get; set; }
    public DateTime? EdocEm { get; set; }

    /// <summary>Internação (FIA): máximo de GREATEST(dt_baixa, dt_alta) processado — ADR-0025.</summary>
    public DateTime? FiaEm { get; set; }

    /// <summary>
    /// CDC de eDoc: último <c>ID_EDOC_MOVIMENTO_LOG</c> processado (poll por PK sequencial —
    /// as tabelas de log não têm índice por data). Null = ainda não ancorado.
    /// </summary>
    public long? EdocLogId { get; set; }
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
    /// Sink de divergências de identidade (origem × hub para o mesmo CPF). Null em cenários
    /// sem persistência (testes) — a detecção simplesmente não é registrada.
    /// </summary>
    public IRegistradorDivergenciasPep? Divergencias { get; init; }

    /// <summary>
    /// Divergências de identidade JÁ CONHECIDAS desta base: CPF → <c>true</c> se o campo em
    /// disputa deve ficar <b>congelado</b> (origem não sobrescreve o hub). Carregado uma vez no
    /// início do run — são poucas centenas, e nada de I/O no caminho quente do upsert.
    ///
    /// <para>Congelam: pendente de arbitragem, não conclusiva, veredicto "hub correto" e
    /// "ambos negados". NÃO congelam (mas continuam conhecidas, para não re-registrar):
    /// veredicto "origem correta" — aí a origem deve mesmo corrigir o hub — e as que um
    /// operador marcou como ignoradas.</para>
    /// </summary>
    public IReadOnlyDictionary<string, bool> DivergenciasConhecidas { get; init; } =
        new Dictionary<string, bool>();

    /// <summary>
    /// Callback para persistir o cursor de retomada (cd_paciente do último bloco
    /// concluído) a cada bloco do modo COMPLETO; recebe <c>null</c> ao terminar a
    /// base inteira. Null em cenários sem persistência (testes).
    /// </summary>
    public Func<long?, CancellationToken, Task>? SalvarCursorPaciente { get; init; }

    /// <summary>
    /// Callback para persistir a marca d'água ao FIM de cada fase concluída (médicos →
    /// pacientes → atendimentos), num contexto isolado, fora da transação do run. A fase
    /// precisa estar INTEIRA concluída antes de avançar a marca — a paginação é por cd, não
    /// por data, então uma marca parcial pularia registros dos blocos não processados.
    /// Null em cenários sem persistência (testes).
    /// </summary>
    public Func<MarcaDagua, CancellationToken, Task>? SalvarMarca { get; init; }
}

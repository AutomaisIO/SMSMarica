using SMSMarica.Core.Integracoes.Pep.Divergencias;
using SMSMarica.Core.Integracoes.Pep.Falhas;
using SMSMarica.Core.Integracoes.Pep.Fhir;
using SMSMarica.Core.Integracoes.Pep.Progresso;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ia;

namespace SMSMarica.Core.Integracoes.Pep.Estrategias;

/// <summary>
/// Estratégia de importação de um tipo de PEP (Salux hoje; MV/Eco no futuro). Cada
/// estratégia conhece o SQL da sua origem e o mapeamento para FHIR R4. O orquestrador
/// resolve a estratégia por <see cref="Tipo"/> a partir da <c>IaFonte</c> selecionada.
/// </summary>
public interface IEstrategiaImportacaoPep
{
    TipoFonte Tipo { get; }

    /// <summary>
    /// Família de bases que esta estratégia atende (<c>IaFonte.Familia</c>), ou null para as
    /// bases sem família. <see cref="Tipo"/> sozinho não basta como discriminador: ele diz o
    /// <b>tipo de banco</b> (SQL Server), não o <b>produto de PEP</b> — dois prontuários
    /// diferentes podem rodar sobre SQL Server, e um casaria com a estratégia do outro.
    /// </summary>
    string? Familia => null;

    /// <summary>Esta estratégia atende esta base?</summary>
    bool Atende(IaFonte fonte) =>
        fonte.Tipo == Tipo
        && string.Equals(fonte.Familia?.Trim(), Familia, StringComparison.OrdinalIgnoreCase);

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
    public DateTime? ProfissionalEm { get; set; }
    public DateTime? PacienteEm { get; set; }
    public DateTime? AtendimentoEm { get; set; }
    public DateTime? DocumentoEm { get; set; }

    /// <summary>Internação (FIA): máximo de GREATEST(dt_baixa, dt_alta) processado — ADR-0025.</summary>
    public DateTime? InternacaoEm { get; set; }

    /// <summary>
    /// CDC de eDoc: último <c>ID_EDOC_MOVIMENTO_LOG</c> processado (poll por PK sequencial —
    /// as tabelas de log não têm índice por data). Null = ainda não ancorado.
    /// </summary>
    public long? LogDocumentoId { get; set; }

    /// <summary>
    /// Ponteiros de CDC <b>numéricos</b> por fase, para origens que não cortam por data — o
    /// Klinikos corta por <c>rv_atualizacao</c> (rowversion do SQL Server: bigint monotônico
    /// global). A chave é o nome da fase (<c>"paciente"</c>, <c>"atendimento"</c>…); quem
    /// escreve e quem lê é a mesma estratégia, então o vocabulário é dela.
    /// </summary>
    public Dictionary<string, long> Ponteiros { get; init; } = [];

    /// <summary>Ponteiro da fase, ou <c>0</c> quando ainda não ancorado (traz tudo).</summary>
    public long Ponteiro(string fase) => Ponteiros.TryGetValue(fase, out var v) ? v : 0;

    /// <summary>Avança o ponteiro da fase — nunca retrocede, mesmo se a origem devolver fora de ordem.</summary>
    public void AvancarPonteiro(string fase, long valor)
    {
        if (valor > Ponteiro(fase)) Ponteiros[fase] = valor;
    }
}

/// <summary>Tudo que a estratégia precisa para rodar um run, mais o canal de progresso.</summary>
public sealed class ContextoImportacaoPep
{
    /// <summary>
    /// Credenciais de conexão DIRETA à origem. Só faz sentido para bases que o servidor alcança
    /// por rede (o Oracle do Salux, pelo túnel). Null nas bases atendidas por agente — ali quem
    /// tem a credencial é o agente, no servidor de destino, e o smsmarica nunca a vê (ADR-0023).
    /// </summary>
    public ConexaoFonte? Conexao { get; init; }

    /// <summary>
    /// Transporte de consulta para bases <b>sem rota direta</b>: o SQL sai daqui, o agente WSS
    /// executa lá e devolve as linhas. Null nas bases de conexão direta.
    ///
    /// <para>Exatamente uma das duas — <see cref="Conexao"/> ou esta — vem preenchida; a
    /// estratégia sabe qual esperar, porque o transporte é característica da base que ela
    /// atende, não uma escolha de runtime.</para>
    /// </summary>
    public Inteligencia.Fontes.IFonteDados? Consulta { get; init; }
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

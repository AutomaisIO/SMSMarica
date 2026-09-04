using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Sisreg;

/// <summary>
/// Configuração global (linha única) da integração com a API-SISREG (DATASUS).
/// Credenciais (senha/token) ficam cifradas em repouso (IDataProtector) e são
/// write-only na API. Parametriza UF/município/centrais para que a integração
/// seja reutilizável por outra prefeitura trocando só a configuração — ver ADR-0012.
/// </summary>
public class SisregConfiguracao
{
    public Guid Id { get; set; }

    /// <summary>Host base do Elasticsearch SISREG. Ex.: <c>https://sisreg-es.saude.gov.br/</c>.</summary>
    public string BaseUrl { get; set; } = "https://sisreg-es.saude.gov.br/";

    /// <summary>Municipal (índice por UF+município) ou Nacional (sufixo <c>-nacional</c>).</summary>
    public EscopoSisreg Escopo { get; set; } = EscopoSisreg.Municipal;

    /// <summary>UF da credencial (ex.: "RJ"). Compõe o nome do índice no escopo municipal.</summary>
    public string Uf { get; set; } = string.Empty;

    /// <summary>Código do município (IBGE, ex.: "3302700" Maricá). Compõe o índice no escopo municipal.</summary>
    public string Municipio { get; set; } = string.Empty;

    /// <summary>
    /// Códigos das centrais reguladoras (campo <c>codigo_central_reguladora</c>), separados
    /// por vírgula. Aplicados como filtro <c>terms</c> em todas as consultas.
    /// </summary>
    public string CentraisReguladoras { get; set; } = string.Empty;

    public TipoAutenticacaoSisreg TipoAutenticacao { get; set; } = TipoAutenticacaoSisreg.Basic;

    /// <summary>Login (usado no esquema Basic).</summary>
    public string? Login { get; set; }

    /// <summary>Senha cifrada (esquema Basic). Write-only na API.</summary>
    public string? SenhaCifrada { get; set; }

    /// <summary>Token cifrado (esquema Bearer/ApiKey). Write-only na API.</summary>
    public string? TokenCifrado { get; set; }

    /// <summary>Integração habilitada. Quando false, as consultas recusam de forma tratada.</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>
    /// <b>Chave-mestra do sincronismo AUTOMÁTICO com o SISREG.</b> Quando false, nenhum agendador
    /// dispara sozinho: nem a varredura diária das unidades, nem o lote de mapeamento
    /// ("sincronizar tudo", inclusive a carga inicial). Ações manuais continuam funcionando —
    /// é um interruptor do que roda sem ninguém pedir, não da integração.
    ///
    /// <para><b>Por que existe, separado do "Desabilitar todas":</b> o botão da tela de sincronismo
    /// grava <c>Ativo=false</c> em cada uma das ~45 linhas de <see cref="SisregVarreduraAgenda"/> —
    /// desligar é destrutivo (perde quem estava ligado e quem não estava) e religar exige reprogramar
    /// a rede. Esta chave só ignora o agendamento; a configuração de cada unidade fica intacta e
    /// religar devolve exatamente o estado anterior.</para>
    ///
    /// <para><b>Por que importa:</b> o SISREG mantém <b>uma sessão por operador</b>. Enquanto o
    /// sincronismo automático roda, qualquer outro uso da mesma credencial (recepção, laboratório de
    /// integração, diagnóstico) derruba e é derrubado. Poder parar tudo com um clique é o que torna
    /// esse trabalho possível sem apagar a programação da rede.</para>
    ///
    /// <para><b>Nasce LIGADO</b> — a coluna entra com default <c>true</c> para não mudar o
    /// comportamento de quem já está em produção.</para>
    /// </summary>
    public bool SincronismoAutomaticoAtivo { get; set; } = true;

    /// <summary>
    /// Por qual porta a importação consulta o cadastro do paciente no CADSUS. Ver
    /// <see cref="FonteCadastroPaciente"/> — é a válvula de escoamento do orçamento anti-robô do
    /// SISREG. Mora aqui, e não na credencial, porque é decisão de operação e a tela já existe.
    /// </summary>
    public FonteCadastroPaciente FonteCadastroPaciente { get; set; } = FonteCadastroPaciente.Sisreg;

    /// <summary>
    /// Quantas consultas de cadastro ao SER correm ao mesmo tempo, cada uma na sua sessão.
    ///
    /// <para><b>1 = como sempre foi</b> (uma sessão, tudo em fila). Acima disso, a importação abre
    /// esse tanto de sessões independentes no SER — <b>com a mesma credencial</b>, o que foi medido
    /// contra o SER real em 28/08/2026: 4 sessões simultâneas logaram sem recusa e cada uma
    /// devolveu o cadastro do CNS que pediu, sem contaminar as outras. É por isso que dá para
    /// paralelizar aqui e <b>não</b> no SISREG, onde a sessão é única por operador.</para>
    ///
    /// <para>Só vale para a consulta de CADASTRO. O resto do motor do SER (varredura, escrita)
    /// continua serializado numa sessão só.</para>
    /// </summary>
    public int ConsultasSimultaneasSer { get; set; } = 1;

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}

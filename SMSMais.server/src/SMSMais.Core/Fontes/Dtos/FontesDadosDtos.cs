namespace SMSMais.Core.Fontes.Dtos;

/// <summary>
/// Uma fonte de dados como a tela Sistema → Fontes de dados a mostra (ADR-0076), venha ela de
/// <c>ia_fonte</c> (bancos, agentes, Klinikos Web, recortes do próprio banco) ou de um sistema com
/// login (SISREG, SER, SERNIT, ESUS SG, e-SUS PEC, API-SISREG). Nunca carrega segredo: só se
/// login/senha/token estão definidos.
/// </summary>
/// <param name="Chave">Identificador na rota: o id (Guid) da base, ou a chave fixa do sistema
///   (<c>sisreg</c>, <c>ser</c>, <c>sernit</c>, <c>esussg</c>, <c>esuspec</c>, <c>sisreg-api</c>).</param>
/// <param name="Origem"><c>base</c> (cadastrável/removível) ou <c>sistema</c> (fixa: existe mesmo
///   antes de configurada).</param>
/// <param name="Categoria"><c>banco</c>, <c>prontuario-web</c>, <c>regulacao</c> ou <c>proprio</c>.</param>
/// <param name="Configurada">Base: sempre. Sistema: tem login e senha (ou token) gravados.</param>
/// <param name="Usuario">Usuário em claro — só onde já era exibido (bases e API-SISREG). Nos sistemas
///   com login de operador ele é write-only, como sempre foi.</param>
/// <param name="UrlPadrao">A URL que o conector usa quando <see cref="BaseUrl"/> está vazio.</param>
/// <param name="Parametros">Parâmetros de CONEXÃO não secretos (ex.: <c>appRoot</c>, <c>cliente</c>).
///   Os de uso ficam no recurso e não vêm aqui.</param>
/// <param name="LoginJunto">Trocar o login exige usuário e senha juntos: o sistema testa o login
///   antes de gravar (SER, SERNIT, ESUS SG).</param>
/// <param name="UsadaPor">Quem usa a fonte — para medir o impacto antes de desativar.</param>
public sealed record FonteDadosDto(
    string Chave,
    string Origem,
    string Nome,
    string Tipo,
    string Categoria,
    string? Slug,
    string? Dialeto,
    string? Ambiente,
    bool Ativo,
    bool Configurada,
    string? Host,
    int? Porta,
    string? Servico,
    string? Usuario,
    string? BaseUrl,
    string? UrlPadrao,
    bool LoginDefinido,
    bool SenhaDefinida,
    bool TokenDefinido,
    bool ViaAgente,
    bool AgenteConectado,
    bool TokenAgenteDefinido,
    string? Familia,
    IReadOnlyDictionary<string, string?> Parametros,
    bool Testavel,
    string? MotivoSemTeste,
    bool LoginJunto,
    IReadOnlyList<string> UsadaPor);

/// <summary>
/// Cadastro de uma BASE (só <c>origem=base</c>; os sistemas com login são fixos). Base SQL Server
/// por agente não nasce aqui: nasce pelo próprio agente (<c>agentes/provisionar</c>).
/// </summary>
public sealed record CriarFonteDadosRequest(
    string Nome,
    string? Slug,
    string Tipo,
    string? Dialeto,
    string Ambiente,
    string? Host,
    int? Porta,
    string? Servico,
    string? Usuario,
    string? Senha,
    string? BaseUrl,
    string? Familia,
    IReadOnlyDictionary<string, string?>? Parametros);

/// <summary>
/// Edição de qualquer fonte. Cada origem usa só os campos que lhe cabem. Nas bases, campo nulo =
/// mantém e texto vazio = limpa. Segredos (senha, token, login dos sistemas) vazios = mantém o atual.
/// <see cref="Parametros"/> só mexe nas chaves de conexão informadas — as de uso, gravadas no mesmo
/// JSON, ficam intactas.
/// </summary>
public sealed record AtualizarFonteDadosRequest(
    string? Nome,
    string? Slug,
    string? Ambiente,
    string? Host,
    int? Porta,
    string? Servico,
    string? Usuario,
    string? Senha,
    string? Token,
    string? BaseUrl,
    string? Familia,
    bool Ativo,
    IReadOnlyDictionary<string, string?>? Parametros);

/// <summary>Login digitado no formulário, para testar ANTES de gravar (SER, SERNIT, ESUS SG).
/// Bases e API-SISREG testam com o que está gravado e ignoram estes campos.</summary>
public sealed record TestarFonteDadosRequest(string? Usuario, string? Senha, string? Cliente);

public sealed record TestarFonteDadosResultado(bool Sucesso, string? Mensagem);

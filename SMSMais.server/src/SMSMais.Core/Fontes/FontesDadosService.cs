using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.EsusSg;
using SMSMais.Core.EsusSg.Dtos;
using SMSMais.Core.Fontes.Dtos;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;
using SMSMais.Core.Integracoes.EsusPec;
using SMSMais.Core.Integracoes.EsusSgWeb;
using SMSMais.Core.Integracoes.Sisreg;
using SMSMais.Core.Integracoes.Sisreg.Configuracao;
using SMSMais.Core.Integracoes.Sisreg.Dtos;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Integracoes.SernitWeb;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Core.Inteligencia.Configuracao;
using SMSMais.Core.Inteligencia.Dtos;
using SMSMais.Core.Ser;
using SMSMais.Core.Sernit;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Fontes;

/// <summary>Cadastro único das fontes de dados — Sistema → Fontes de dados (ADR-0077).</summary>
public interface IFontesDadosService
{
    Task<IReadOnlyList<FonteDadosDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<FonteDadosDto> ObterAsync(string chave, CancellationToken cancellationToken = default);
    Task<Guid> CriarAsync(CriarFonteDadosRequest request, CancellationToken cancellationToken = default);
    Task AtualizarAsync(string chave, AtualizarFonteDadosRequest request, CancellationToken cancellationToken = default);
    Task RemoverAsync(string chave, CancellationToken cancellationToken = default);
    Task<TestarFonteDadosResultado> TestarAsync(string chave, TestarFonteDadosRequest request, CancellationToken cancellationToken = default);
    Task<TokenAgenteGerado> GerarTokenAgenteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TokenAgenteGerado> ProvisionarAgenteAsync(string slug, string? nome, CancellationToken cancellationToken = default);
}

/// <summary>
/// Fachada da Entrega 1 do ADR-0077: uma lista e uma edição para todas as fontes, sem mudar onde o
/// dado mora. Bases continuam em <c>ia_fonte</c> (via <see cref="IIaFonteService"/>); os logins de
/// SISREG/SER/SERNIT/ESUS SG/e-SUS PEC em <c>integracao_credencial</c>; a API-SISREG em
/// <c>sisreg_configuracao</c>. Os motores leem de onde sempre leram.
///
/// <para><b>Conexão × uso no mesmo JSON.</b> A credencial guarda no <c>ParametrosJson</c> também a
/// agenda dos motores, e a fonte Klinikos Web guarda a cadência da importação. Esta fachada só lê e
/// grava as chaves de CONEXÃO (<see cref="SistemaFonte.ChavesConexao"/>,
/// <see cref="ChavesConexaoKlinikos"/>) e devolve o resto intacto.</para>
///
/// <para><b>Trocar o login do SER, SERNIT e ESUS SG delega aos <c>*MotorService</c></b>: eles testam
/// o login antes de gravar e reiniciam a sessão. Credencial que não entra faria o motor falhar de
/// madrugada.</para>
/// </summary>
public sealed class FontesDadosService(
    SmsMaisDbContext db,
    IIaFonteService bases,
    IIntegracaoCredencialService credenciais,
    ISisregConfiguracaoService sisregApi,
    ISisregConsultaService sisregConsulta,
    ISerMotorService serMotor,
    ISernitMotorService sernitMotor,
    IEsusSgMotorService esusSgMotor,
    ISerWebSessao serSessao,
    ISernitWebSessao sernitSessao,
    IEsusSgSessao esusSgSessao) : IFontesDadosService
{
    /// <summary>Chaves de CONEXÃO da fonte Klinikos Web. As demais do mesmo JSON (<c>webPrimaria</c>,
    /// <c>periodico*</c>, <c>deepThrottleSeg</c>) são uso da importação e ficam em Importar Prontuários.</summary>
    public static readonly IReadOnlyList<string> ChavesConexaoKlinikos = ["appRoot", "unidCodigo", "metaSource", "build"];

    /// <summary>Chaves que o JSON guarda como booleano (o resto é texto).</summary>
    private static readonly HashSet<string> ChavesBooleanas = ["autoLogin"];

    private static readonly TipoFonte[] TiposCadastraveis =
        [TipoFonte.Salux, TipoFonte.Mv, TipoFonte.Eco, TipoFonte.Fhir, TipoFonte.Postgres,
         TipoFonte.Regulacao, TipoFonte.Atendimento, TipoFonte.KlinikosWeb];

    public async Task<IReadOnlyList<FonteDadosDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var lista = new List<FonteDadosDto>();
        var usos = await UsosDasBasesAsync(cancellationToken);
        foreach (var b in await bases.ListarAsync(cancellationToken))
        {
            lista.Add(DeBase(b, usos.GetValueOrDefault(b.Id) ?? []));
        }

        foreach (var s in SistemasFonte.Todos)
        {
            lista.Add(await DeSistemaAsync(s, cancellationToken));
        }

        return lista;
    }

    public async Task<FonteDadosDto> ObterAsync(string chave, CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(chave, out var id))
        {
            var b = (await bases.ListarAsync(cancellationToken)).FirstOrDefault(x => x.Id == id)
                ?? throw new NaoEncontradoException("FonteDados", id);
            var usos = await UsosDasBasesAsync(cancellationToken);
            return DeBase(b, usos.GetValueOrDefault(id) ?? []);
        }

        return await DeSistemaAsync(Sistema(chave), cancellationToken);
    }

    public async Task<Guid> CriarAsync(CriarFonteDadosRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<TipoFonte>(request.Tipo, ignoreCase: true, out var tipo) || !TiposCadastraveis.Contains(tipo))
        {
            throw new ValidacaoException("fonte.tipo_invalido",
                tipo == TipoFonte.SqlServer
                    ? "Base SQL Server nasce pelo agente instalado no servidor dela (ele se cadastra sozinho)."
                    : $"Tipo de fonte inválido: '{request.Tipo}'.");
        }

        var parametros = tipo == TipoFonte.KlinikosWeb
            ? MesclarChaves(null, request.Parametros, ChavesConexaoKlinikos)
            : null;

        return await bases.CadastrarAsync(new CadastrarFonteRequest(
            request.Nome, request.Slug, tipo.ToString(),
            request.Dialeto ?? DialetoPadrao(tipo), request.Ambiente,
            request.Host, request.Porta, request.Servico, request.Usuario, request.Senha, request.BaseUrl,
            ViaAgente: false,
            Familia: tipo == TipoFonte.KlinikosWeb && string.IsNullOrWhiteSpace(request.Familia) ? "klinikos" : request.Familia,
            ParametrosJson: parametros), cancellationToken);
    }

    public async Task AtualizarAsync(string chave, AtualizarFonteDadosRequest request, CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(chave, out var id))
        {
            await AtualizarBaseAsync(id, request, cancellationToken);
            return;
        }

        var sistema = Sistema(chave);
        if (sistema.Chave == SistemasFonte.SisregApi)
        {
            await AtualizarSisregApiAsync(request, cancellationToken);
            return;
        }

        await AtualizarSistemaAsync(sistema, request, cancellationToken);
    }

    public async Task RemoverAsync(string chave, CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(chave, out var id))
        {
            await bases.RemoverAsync(id, cancellationToken);
            return;
        }

        var sistema = Sistema(chave);
        if (sistema.Chave == SistemasFonte.SisregApi)
        {
            throw new ValidacaoException("fonte.sisreg_api_fixa",
                "A API do SISREG não se remove: desmarque \"Ativa\" para desligar.");
        }

        // Limpa login e senha e desativa; o JSON (com a agenda dos motores) fica.
        await credenciais.LimparAsync(sistema.Chave, cancellationToken);
        ReiniciarSessao(sistema.Chave);
    }

    public async Task<TestarFonteDadosResultado> TestarAsync(
        string chave, TestarFonteDadosRequest request, CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(chave, out var id))
        {
            var b = await db.IaFontes.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id && f.ExcluidoEm == null, cancellationToken)
                ?? throw new NaoEncontradoException("FonteDados", id);
            var motivo = MotivoSemTesteBase(b.Tipo, b.Dialeto, b.ViaAgente);
            if (motivo is not null) return new TestarFonteDadosResultado(false, motivo);

            var r = await bases.TestarConexaoAsync(id, cancellationToken);
            return new TestarFonteDadosResultado(r.Sucesso, r.Mensagem);
        }

        var sistema = Sistema(chave);
        if (sistema.MotivoSemTeste is not null) return new TestarFonteDadosResultado(false, sistema.MotivoSemTeste);

        try
        {
            switch (sistema.Chave)
            {
                case SistemasFonte.SisregApi:
                    var fila = await sisregConsulta.FilaSolicitacoesAmbulatoriaisAsync(1, cancellationToken);
                    return new TestarFonteDadosResultado(true, $"Conexão OK. {fila.Total} registro(s) na fila.");
                case SerWebSessao.Provedor:
                    await serMotor.TestarCredencialAsync(request.Usuario ?? "", request.Senha ?? "", cancellationToken);
                    return new TestarFonteDadosResultado(true, "O SER aceitou o login.");
                case SernitWebSessao.Provedor:
                    await sernitMotor.TestarCredencialAsync(request.Usuario ?? "", request.Senha ?? "", cancellationToken);
                    return new TestarFonteDadosResultado(true, "O SERNIT aceitou o login.");
                case EsusSgSessao.Provedor:
                    var msg = await esusSgMotor.TestarCredencialAsync(
                        new EsusSgCredencialRequest(request.Usuario ?? "", request.Senha ?? "", request.Cliente), cancellationToken);
                    return new TestarFonteDadosResultado(true, msg);
                default:
                    return new TestarFonteDadosResultado(false, "Sem teste para esta fonte.");
            }
        }
        catch (ValidacaoException ex)
        {
            return new TestarFonteDadosResultado(false, ex.Message);
        }
        catch (ConflitoException ex)
        {
            return new TestarFonteDadosResultado(false, ex.Message);
        }
    }

    public Task<TokenAgenteGerado> GerarTokenAgenteAsync(Guid id, CancellationToken cancellationToken = default) =>
        bases.GerarTokenAgenteAsync(id, cancellationToken);

    public Task<TokenAgenteGerado> ProvisionarAgenteAsync(string slug, string? nome, CancellationToken cancellationToken = default) =>
        bases.ProvisionarAgenteAsync(slug, nome, cancellationToken);

    // ---- Bases (ia_fonte) ----

    private async Task AtualizarBaseAsync(Guid id, AtualizarFonteDadosRequest request, CancellationToken cancellationToken)
    {
        var atual = await db.IaFontes.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id && f.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException("FonteDados", id);

        // Klinikos Web: só as chaves de conexão; a cadência (uso) gravada no mesmo JSON fica intacta.
        var parametros = atual.Tipo == TipoFonte.KlinikosWeb
            ? MesclarChaves(atual.ParametrosJson, request.Parametros, ChavesConexaoKlinikos)
            : atual.ParametrosJson;

        // Campo ausente (nulo) mantém o gravado; texto vazio limpa. Sem isso, um cliente que mandasse
        // só a conexão apagaria a família — e a importação da UPA/Santa Rita deixaria de reconhecer a
        // base (a estratégia casa por tipo + família), calada.
        await bases.AtualizarAsync(id, new AtualizarFonteRequest(
            Nome: string.IsNullOrWhiteSpace(request.Nome) ? atual.Nome : request.Nome,
            Slug: Manter(request.Slug, atual.Slug),
            Ambiente: string.IsNullOrWhiteSpace(request.Ambiente) ? atual.Ambiente.ToString() : request.Ambiente,
            Host: Manter(request.Host, atual.Host),
            Porta: request.Porta ?? atual.Porta,
            Servico: Manter(request.Servico, atual.Servico),
            Usuario: Manter(request.Usuario, atual.Usuario),
            Senha: request.Senha,
            BaseUrl: Manter(request.BaseUrl, atual.BaseUrl),
            Ativo: request.Ativo,
            Familia: Manter(request.Familia, atual.Familia),
            ParametrosJson: parametros), cancellationToken);
    }

    private static string? Manter(string? novo, string? atual) => novo is null ? atual : novo;

    private FonteDadosDto DeBase(FonteDetalheDto b, IReadOnlyList<string> usadaPor)
    {
        var tipo = Enum.TryParse<TipoFonte>(b.Tipo, out var t) ? t : (TipoFonte?)null;
        var dialeto = Enum.TryParse<DialetoSql>(b.Dialeto, out var d) ? d : DialetoSql.Nenhum;
        var motivo = tipo is null ? "Tipo desconhecido." : MotivoSemTesteBase(tipo.Value, dialeto, b.ViaAgente);

        var categoria = tipo switch
        {
            TipoFonte.KlinikosWeb => "prontuario-web",
            TipoFonte.Regulacao or TipoFonte.Atendimento => "proprio",
            _ => "banco",
        };

        var parametros = tipo == TipoFonte.KlinikosWeb
            ? LerChaves(b.ParametrosJson, ChavesConexaoKlinikos)
            : new Dictionary<string, string?>();

        return new FonteDadosDto(
            Chave: b.Id.ToString(),
            Origem: "base",
            Nome: b.Nome,
            Tipo: b.Tipo,
            Categoria: categoria,
            Slug: b.Slug,
            Dialeto: b.Dialeto,
            Ambiente: b.Ambiente,
            Ativo: b.Ativo,
            Configurada: true,
            Host: b.Host,
            Porta: b.Porta,
            Servico: b.Servico,
            Usuario: b.Usuario,
            BaseUrl: b.BaseUrl,
            UrlPadrao: null,
            LoginDefinido: !string.IsNullOrWhiteSpace(b.Usuario),
            SenhaDefinida: b.SenhaDefinida,
            TokenDefinido: false,
            ViaAgente: b.ViaAgente,
            AgenteConectado: b.AgenteConectado,
            TokenAgenteDefinido: b.TokenDefinido,
            Familia: b.Familia,
            Parametros: parametros,
            Testavel: motivo is null,
            MotivoSemTeste: motivo,
            LoginJunto: false,
            UsadaPor: usadaPor);
    }

    private static string? MotivoSemTesteBase(TipoFonte tipo, DialetoSql dialeto, bool viaAgente) => tipo switch
    {
        TipoFonte.KlinikosWeb =>
            "Sessão única por usuário no Klinikos: testar derrubaria quem estiver logado com o mesmo usuário.",
        _ when viaAgente => null,
        _ when dialeto == DialetoSql.Nenhum => "Fonte sem SQL: não há teste de conexão para ela.",
        _ => null,
    };

    private static string DialetoPadrao(TipoFonte tipo) => tipo switch
    {
        TipoFonte.Postgres or TipoFonte.Regulacao or TipoFonte.Atendimento => nameof(DialetoSql.Postgres),
        TipoFonte.Fhir or TipoFonte.KlinikosWeb => nameof(DialetoSql.Nenhum),
        _ => nameof(DialetoSql.Oracle),
    };

    /// <summary>Quem usa cada base: Importar Prontuários (agenda), Indicadores e a Consulta Inteligente.</summary>
    private async Task<Dictionary<Guid, IReadOnlyList<string>>> UsosDasBasesAsync(CancellationToken cancellationToken)
    {
        var fontes = await db.IaFontes.AsNoTracking()
            .Where(f => f.ExcluidoEm == null)
            .Select(f => new { f.Id, f.Tipo, f.Dialeto, f.ViaAgente, f.Ativo })
            .ToListAsync(cancellationToken);
        var agendas = await db.PepSincronizacaoAgendas.AsNoTracking()
            .ToDictionaryAsync(a => a.FonteId, a => new { a.Ativo, a.IntervaloMinutos }, cancellationToken);
        var indicadores = await db.Indicadores.AsNoTracking()
            .Where(i => i.ExcluidoEm == null && i.FonteId != null)
            .GroupBy(i => i.FonteId!.Value)
            .Select(g => new { FonteId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.FonteId, x => x.Total, cancellationToken);

        var resultado = new Dictionary<Guid, IReadOnlyList<string>>();
        foreach (var f in fontes)
        {
            var usos = new List<string>();
            if (agendas.TryGetValue(f.Id, out var agenda))
            {
                usos.Add(agenda.Ativo
                    ? $"Importar Prontuários (a cada {agenda.IntervaloMinutos} min)"
                    : "Importar Prontuários (agenda desligada)");
            }
            else if (f.Tipo == TipoFonte.KlinikosWeb)
            {
                usos.Add("Importar Prontuários (conector web)");
            }

            if (indicadores.TryGetValue(f.Id, out var n))
            {
                usos.Add(n == 1 ? "Indicadores (1 indicador)" : $"Indicadores ({n} indicadores)");
            }

            if (f.Dialeto != DialetoSql.Nenhum || f.ViaAgente)
            {
                usos.Add("Consulta Inteligente");
            }

            resultado[f.Id] = usos;
        }

        return resultado;
    }

    // ---- Sistemas com login (integracao_credencial / sisreg_configuracao) ----

    private static SistemaFonte Sistema(string chave) =>
        SistemasFonte.Obter((chave ?? string.Empty).Trim())
            ?? throw new NaoEncontradoException("FonteDados", chave ?? string.Empty);

    private async Task<FonteDadosDto> DeSistemaAsync(SistemaFonte s, CancellationToken cancellationToken)
    {
        if (s.Chave == SistemasFonte.SisregApi)
        {
            // Lê sem criar: listar a tela não pode inventar a linha de configuração.
            var cfg = await db.SisregConfiguracoes.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
            var senha = !string.IsNullOrEmpty(cfg?.SenhaCifrada);
            var token = !string.IsNullOrEmpty(cfg?.TokenCifrado);
            return DtoDeSistema(s,
                ativo: cfg?.Ativo ?? false,
                configurada: cfg is not null && (token || (!string.IsNullOrWhiteSpace(cfg.Login) && senha)),
                usuario: cfg?.Login,
                baseUrl: cfg?.BaseUrl,
                loginDefinido: !string.IsNullOrWhiteSpace(cfg?.Login),
                senhaDefinida: senha,
                tokenDefinido: token,
                parametros: new Dictionary<string, string?>
                {
                    ["tipoAutenticacao"] = (cfg?.TipoAutenticacao ?? TipoAutenticacaoSisreg.Basic).ToString(),
                });
        }

        var c = await credenciais.ObterAsync(s.Chave, cancellationToken);
        var chaves = LerChaves(c.ParametrosJson, s.ChavesConexao);
        chaves.Remove("baseUrl", out var baseUrl);
        return DtoDeSistema(s,
            ativo: c.Ativo,
            configurada: c.ClientIdDefinido && c.ClientSecretDefinido,
            usuario: null, // login de operador é write-only, como sempre foi
            baseUrl: baseUrl,
            loginDefinido: c.ClientIdDefinido,
            senhaDefinida: c.ClientSecretDefinido,
            tokenDefinido: false,
            parametros: chaves);
    }

    private static FonteDadosDto DtoDeSistema(
        SistemaFonte s, bool ativo, bool configurada, string? usuario, string? baseUrl,
        bool loginDefinido, bool senhaDefinida, bool tokenDefinido, IReadOnlyDictionary<string, string?> parametros) => new(
        Chave: s.Chave,
        Origem: "sistema",
        Nome: s.Nome,
        Tipo: s.Tipo,
        Categoria: "regulacao",
        Slug: null,
        Dialeto: null,
        Ambiente: nameof(AmbienteFonte.Producao),
        Ativo: ativo,
        Configurada: configurada,
        Host: null,
        Porta: null,
        Servico: null,
        Usuario: usuario,
        BaseUrl: baseUrl,
        UrlPadrao: s.UrlPadrao,
        LoginDefinido: loginDefinido,
        SenhaDefinida: senhaDefinida,
        TokenDefinido: tokenDefinido,
        ViaAgente: false,
        AgenteConectado: false,
        TokenAgenteDefinido: false,
        Familia: null,
        Parametros: parametros,
        Testavel: s.MotivoSemTeste is null,
        MotivoSemTeste: s.MotivoSemTeste,
        LoginJunto: s.LoginJunto,
        UsadaPor: s.UsadaPor);

    private async Task AtualizarSisregApiAsync(AtualizarFonteDadosRequest request, CancellationToken cancellationToken)
    {
        var atual = await db.SisregConfiguracoes.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var tipoAuth = atual?.TipoAutenticacao ?? TipoAutenticacaoSisreg.Basic;
        if (request.Parametros?.TryGetValue("tipoAutenticacao", out var t) == true && !string.IsNullOrWhiteSpace(t))
        {
            tipoAuth = Enum.TryParse<TipoAutenticacaoSisreg>(t, ignoreCase: true, out var v)
                ? v
                : throw new ValidacaoException("fonte.tipo_autenticacao", $"Tipo de autenticação inválido: '{t}'.");
        }

        await sisregApi.AtualizarCredencialAsync(new AtualizarCredencialSisregApiRequest(
            BaseUrl: string.IsNullOrWhiteSpace(request.BaseUrl) ? atual?.BaseUrl ?? new SisregConfiguracao().BaseUrl : request.BaseUrl,
            TipoAutenticacao: tipoAuth,
            Login: request.Usuario,
            Senha: request.Senha,
            Token: request.Token,
            Ativo: request.Ativo), cancellationToken);
    }

    private async Task AtualizarSistemaAsync(SistemaFonte s, AtualizarFonteDadosRequest request, CancellationToken cancellationToken)
    {
        var usuario = string.IsNullOrWhiteSpace(request.Usuario) ? null : request.Usuario.Trim();
        var senha = string.IsNullOrWhiteSpace(request.Senha) ? null : request.Senha;
        var trocaLogin = usuario is not null || senha is not null;

        // 1) Login. Nos que testam antes de gravar, delega ao motor (testa → grava → reinicia).
        if (trocaLogin && s.LoginJunto)
        {
            if (usuario is null || senha is null)
            {
                throw new ValidacaoException("fonte.login_incompleto",
                    "Para trocar o login informe usuário e senha juntos — o sistema testa o login antes de gravar.");
            }

            switch (s.Chave)
            {
                case SerWebSessao.Provedor:
                    await serMotor.SalvarCredencialAsync(usuario, senha, cancellationToken);
                    break;
                case SernitWebSessao.Provedor:
                    await sernitMotor.SalvarCredencialAsync(usuario, senha, cancellationToken);
                    break;
                case EsusSgSessao.Provedor:
                    var cliente = request.Parametros?.GetValueOrDefault("cliente");
                    await esusSgMotor.SalvarCredencialAsync(new EsusSgCredencialRequest(usuario, senha, cliente), cancellationToken);
                    break;
            }
        }

        // 2) Conexão não secreta + ativo, preservando no JSON tudo que é uso dos motores.
        var atual = await credenciais.ObterAsync(s.Chave, cancellationToken);
        var novos = new Dictionary<string, string?>(request.Parametros ?? new Dictionary<string, string?>());
        // Mesma regra das bases: URL ausente mantém; vazia remove a chave (o conector volta ao padrão).
        if (s.ChavesConexao.Contains("baseUrl") && request.BaseUrl is not null)
        {
            novos["baseUrl"] = request.BaseUrl;
        }
        if (s.Chave == EsusSgSessao.Provedor && novos.TryGetValue("cliente", out var cli) && !string.IsNullOrWhiteSpace(cli))
        {
            novos["cliente"] = cli.Trim().ToUpperInvariant();
        }
        var json = MesclarChaves(atual.ParametrosJson, novos, s.ChavesConexao);

        // e-SUS PEC: o login é o CPF, guardado só com dígitos (como o cartão antigo fazia).
        var clientId = !trocaLogin || s.LoginJunto
            ? null
            : s.Chave == CorrecaoTelefoneEsusService.Provedor && usuario is not null
                ? new string(usuario.Where(char.IsDigit).ToArray())
                : usuario;

        await credenciais.AtualizarAsync(s.Chave, new AtualizarIntegracaoCredencialRequest(
            ClientId: clientId,
            ClientSecret: !trocaLogin || s.LoginJunto ? null : senha,
            RedirectUri: atual.RedirectUri,
            ParametrosJson: json ?? string.Empty,
            Ativo: request.Ativo), cancellationToken);

        // URL/cliente novos só valem num login novo: a sessão em memória ainda aponta para o antigo.
        if (!JsonIgual(atual.ParametrosJson, json))
        {
            ReiniciarSessao(s.Chave);
        }
    }

    private void ReiniciarSessao(string chave)
    {
        switch (chave)
        {
            case SerWebSessao.Provedor: serSessao.Reiniciar(); break;
            case SernitWebSessao.Provedor: sernitSessao.Reiniciar(); break;
            case EsusSgSessao.Provedor: esusSgSessao.Reiniciar(); break;
            // SISREG relê a credencial a cada chamada; o e-SUS PEC abre sessão por rodada.
        }
    }

    // ---- JSON: só as chaves de conexão, o resto intacto ----

    /// <summary>
    /// Aplica em <paramref name="jsonAtual"/> só as chaves de <paramref name="permitidas"/> que vieram
    /// em <paramref name="novos"/> (vazio/nulo = remove a chave, para o conector cair no padrão). As
    /// demais chaves do JSON — o uso dos motores — passam intactas. Chave fora das permitidas é erro:
    /// é a tela de Fontes de dados tentando gravar uso. Devolve nulo se o JSON ficar vazio.
    /// </summary>
    public static string? MesclarChaves(
        string? jsonAtual, IReadOnlyDictionary<string, string?>? novos, IReadOnlyList<string> permitidas)
    {
        JsonObject obj;
        try
        {
            obj = string.IsNullOrWhiteSpace(jsonAtual)
                ? new JsonObject()
                : JsonNode.Parse(jsonAtual) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            // JSON corrompido já não era lido por ninguém: recomeça só com a conexão.
            obj = new JsonObject();
        }

        foreach (var (chave, valor) in novos ?? new Dictionary<string, string?>())
        {
            if (!permitidas.Contains(chave))
            {
                throw new ValidacaoException("fonte.parametro_invalido",
                    $"'{chave}' não é parâmetro de conexão desta fonte (o uso é configurado no recurso que a usa).");
            }

            if (string.IsNullOrWhiteSpace(valor))
            {
                obj.Remove(chave);
            }
            else if (ChavesBooleanas.Contains(chave))
            {
                obj[chave] = bool.TryParse(valor, out var b)
                    ? b
                    : throw new ValidacaoException("fonte.parametro_invalido", $"'{chave}' deve ser verdadeiro ou falso.");
            }
            else
            {
                obj[chave] = valor.Trim();
            }
        }

        return obj.Count == 0 ? null : obj.ToJsonString();
    }

    /// <summary>Lê do JSON só as chaves pedidas, como texto (booleano vira "true"/"false").</summary>
    public static Dictionary<string, string?> LerChaves(string? json, IReadOnlyList<string> chaves)
    {
        var resultado = chaves.ToDictionary(c => c, _ => (string?)null);
        if (string.IsNullOrWhiteSpace(json)) return resultado;

        try
        {
            if (JsonNode.Parse(json) is not JsonObject obj) return resultado;
            foreach (var chave in chaves)
            {
                if (obj[chave] is JsonValue v)
                {
                    resultado[chave] = v.TryGetValue<bool>(out var b) ? (b ? "true" : "false") : v.ToString();
                }
            }
        }
        catch (JsonException)
        {
            // JSON corrompido: mostra vazio, o conector também cai no padrão.
        }

        return resultado;
    }

    private static bool JsonIgual(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return string.IsNullOrWhiteSpace(a) == string.IsNullOrWhiteSpace(b);
        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(a), JsonNode.Parse(b));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

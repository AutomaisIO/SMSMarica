using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Conversas;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.EsusPec;
using SMSMais.Core.Pacientes.Enriquecimento;

namespace SMSMais.Core.Cidadao.Entrada;

/// <summary>Quem está entrando: o CPF e a data de nascimento que ela digitou (já conferidos).</summary>
public sealed record PessoaNaEntrada(string Cpf, DateOnly Nascimento);

/// <summary>
/// "Nenhum desses é meu" → os celulares do CPF no e-SUS PEC, com a conta da PLATAFORMA (Fontes de dados).
/// Devolve os números (55 + DDD + número) que são celular; lista vazia quando o e-SUS não tem a pessoa,
/// ou tem outra pessoa no CPF. Indisponível (conta em uso, fora do ar, janela da rotina da madrugada,
/// login da conta falhando) → <see cref="ConflitoException"/> <c>esus.indisponivel</c>.
/// </summary>
public interface ITelefonesEsusNaEntrada
{
    /// <summary>A conta da plataforma está em Fontes de dados? Sem ela, a opção nem aparece.</summary>
    Task<bool> DisponivelAsync(CancellationToken ct = default);

    Task<IReadOnlyList<string>> BuscarAsync(PessoaNaEntrada pessoa, CancellationToken ct = default);
}

/// <summary>
/// As travas do ADR-0078 §4: o PEC é sessão única por conta, então uma consulta por vez e nunca forçando
/// a entrada; fora da janela da rotina da madrugada (que força); disjuntor de 30 minutos depois de um
/// login recusado (senha trocada não pode virar conta bloqueada no PEC); resultado guardado por CPF.
/// </summary>
public sealed class TelefonesEsusNaEntrada(
    IIntegracaoCredencialService credenciais,
    IConsultaFichaEsusPec esus,
    IMemoryCache cache,
    ILogger<TelefonesEsusNaEntrada> logger) : ITelefonesEsusNaEntrada
{
    internal static readonly TimeSpan EsperaPelaVez = TimeSpan.FromSeconds(20);
    internal static readonly TimeSpan PausaAposLoginRecusado = TimeSpan.FromMinutes(30);
    internal static readonly TimeSpan GuardaPorCpf = TimeSpan.FromMinutes(20);

    private static readonly SemaphoreSlim UmaPorVez = new(1, 1);
    private static DateTime _pausadoAte = DateTime.MinValue;

    private const string MensagemIndisponivel =
        "Não conseguimos procurar outros números agora. Tente de novo em instantes ou procure o posto de saúde.";

    public async Task<bool> DisponivelAsync(CancellationToken ct = default)
    {
        var c = await credenciais.ObterAsync(CorrecaoTelefoneEsusService.Provedor, ct);
        return c.ClientIdDefinido && c.ClientSecretDefinido && c.Ativo;
    }

    public async Task<IReadOnlyList<string>> BuscarAsync(PessoaNaEntrada pessoa, CancellationToken ct = default)
    {
        if (cache.TryGetValue(Chave(pessoa.Cpf), out IReadOnlyList<string>? guardado) && guardado is not null)
            return guardado;

        if (DateTime.UtcNow < _pausadoAte) throw Indisponivel("login da conta recusado há pouco");

        IntegracaoCredencialContexto cred;
        try { cred = await credenciais.ObterContextoAsync(CorrecaoTelefoneEsusService.Provedor, ct); }
        catch (ValidacaoException) { throw Indisponivel("conta da plataforma não configurada"); }

        if (string.IsNullOrWhiteSpace(cred.ClientId) || string.IsNullOrWhiteSpace(cred.ClientSecret))
            throw Indisponivel("conta da plataforma sem login");
        var usuario = cred.ClientId;
        var senha = cred.ClientSecret;

        var p = CorrecaoTelefoneEsusService.Parametros.Ler(cred.ParametrosJson);
        var agora = TimeOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        if (CorrecaoTelefoneEsusService.DentroDaJanela(p.Inicio, p.Fim, agora))
            throw Indisponivel("janela da rotina da madrugada");

        if (!await UmaPorVez.WaitAsync(EsperaPelaVez, ct)) throw Indisponivel("outra consulta em andamento");
        try
        {
            ResultadoConsultaEsusPec r;
            try
            {
                r = await esus.ConsultarAsync(
                    new ContaEsusPec(p.BaseUrl, usuario, senha, Forcar: false, p.AcessoId), pessoa.Cpf, ct);
            }
            catch (ErroEsusPec ex) when (ex.Message.StartsWith("Login:", StringComparison.Ordinal))
            {
                _pausadoAte = DateTime.UtcNow + PausaAposLoginRecusado;
                logger.LogError("Entrada do cidadão: o e-SUS recusou o login da conta da plataforma ({Erro}). "
                    + "Consulta pausada por {Minutos} min.", ex.Message, PausaAposLoginRecusado.TotalMinutes);
                throw Indisponivel("login recusado");
            }
            catch (Exception ex) when (ex is ErroEsusPec or HttpRequestException or TaskCanceledException
                                           && !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Entrada do cidadão: a consulta ao e-SUS falhou.");
                throw Indisponivel("falha na consulta");
            }

            if (r.OutraSessaoAberta) throw Indisponivel("conta da plataforma em uso");

            var numeros = Celulares(r.Ficha, pessoa);
            cache.Set(Chave(pessoa.Cpf), numeros, GuardaPorCpf);
            return numeros;
        }
        finally
        {
            UmaPorVez.Release();
        }
    }

    /// <summary>
    /// Só vale a mesma pessoa: o mesmo nascimento que ela digitou (o CPF da mãe na ficha da criança não
    /// passa), sem óbito e com o cadastro ativo no e-SUS. Fora disso, nenhum número.
    /// </summary>
    internal static IReadOnlyList<string> Celulares(FichaCidadaoEsusPec? ficha, PessoaNaEntrada pessoa)
    {
        if (ficha is null || ficha.Faleceu || !ficha.Ativo) return [];

        var externa = FichaExterna.DoEsus(ficha);
        if (externa.DataNascimento != pessoa.Nascimento) return [];

        var numeros = new List<string>();
        foreach (var t in externa.Telefones)
        {
            var canonico = TelefoneWhatsApp.Canonizar(t.Numero);
            if (TelefoneWhatsApp.EhCelularBr(canonico) && !numeros.Any(n => TelefoneWhatsApp.MesmoNumero(n, canonico)))
                numeros.Add(canonico);
        }
        return numeros;
    }

    /// <summary>Só para os testes: zera o disjuntor entre um caso e outro.</summary>
    internal static void ReiniciarPausa() => _pausadoAte = DateTime.MinValue;

    private ConflitoException Indisponivel(string motivo)
    {
        logger.LogInformation("Entrada do cidadão: e-SUS não consultado ({Motivo}).", motivo);
        return new ConflitoException("esus.indisponivel", MensagemIndisponivel);
    }

    private static string Chave(string cpf) => $"entrada:esus:{cpf}";
}

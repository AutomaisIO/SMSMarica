using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Ser.Sessao;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// O ciclo de vida da sessão de ESCRITA no SER.
///
/// <para>A regra que estes testes protegem: a credencial pessoal do operador no sistema do
/// Estado é amarrada à <b>sessão</b> do SMSMais (o <c>jti</c> do token), não ao usuário. Se
/// alguém trocar a chave para o id do usuário "porque é mais simples", sair e voltar passa a
/// herdar a credencial da sessão anterior — e "sair" deixa de significar sair.</para>
/// </summary>
public class SerSessaoOperadorStoreTests
{
    private static readonly Guid Operador = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static SerSessaoOperadorStore Novo(out List<SessaoFalsa> criadas)
    {
        var lista = new List<SessaoFalsa>();
        criadas = lista;
        return new SerSessaoOperadorStore(
            NullLogger<SerSessaoOperadorStore>.Instance,
            (usuario, senha) =>
            {
                var s = new SessaoFalsa(usuario, senha);
                lista.Add(s);
                return s;
            });
    }

    [Fact]
    public async Task Sessao_nova_do_MESMO_usuario_nao_herda_a_credencial_da_anterior()
    {
        using var store = Novo(out _);
        await store.AutenticarAsync("jti-do-primeiro-login", Operador, "56840827", "senha", default);

        store.Estado("jti-do-primeiro-login").Autenticado.Should().BeTrue();

        // Mesmo usuário, login novo = jti novo. Tem de começar sem credencial do SER.
        store.Estado("jti-do-segundo-login").Autenticado.Should().BeFalse(
            "sair e entrar de novo não pode reaproveitar a senha do SER da sessão anterior");
    }

    [Fact]
    public async Task Encerrar_derruba_a_sessao_e_fecha_o_cookie_jar_do_SER()
    {
        using var store = Novo(out var criadas);
        await store.AutenticarAsync("jti-a", Operador, "56840827", "senha", default);

        store.Encerrar("jti-a");

        store.Estado("jti-a").Autenticado.Should().BeFalse();
        // Não basta esquecer a entrada: o cliente HTTP segura o cookie jar autenticado no SER.
        criadas.Single().Descartada.Should().BeTrue();
    }

    [Fact]
    public async Task Exigir_recusa_com_codigo_que_a_tela_usa_para_pedir_a_senha()
    {
        using var store = Novo(out _);
        await store.AutenticarAsync("jti-a", Operador, "56840827", "senha", default);

        var act = () => store.Exigir("jti-de-outra-sessao");

        // O código importa: é por ele que o front distingue "precisa autenticar no SER" de um
        // erro de validação qualquer, e abre o modal em vez de mostrar erro seco.
        act.Should().Throw<ValidacaoException>()
            .Which.Erros.Should().ContainKey("ser.sessao_operador_ausente");
    }

    [Fact]
    public async Task Reautenticar_na_MESMA_sessao_descarta_a_conexao_anterior()
    {
        using var store = Novo(out var criadas);
        await store.AutenticarAsync("jti-a", Operador, "56840827", "senha-velha", default);
        await store.AutenticarAsync("jti-a", Operador, "56840827", "senha-nova", default);

        // Duas sessões vivas do mesmo operador seriam duas conversas Seam concorrentes no SER,
        // e qual delas "vence" seria imprevisível.
        criadas.Should().HaveCount(2);
        criadas[0].Descartada.Should().BeTrue();
        criadas[1].Descartada.Should().BeFalse();
        store.Estado("jti-a").UsuarioSer.Should().Be("56840827");
    }

    [Fact]
    public async Task Credencial_recusada_pelo_SER_nao_entra_no_store()
    {
        var criadas = new List<SessaoFalsa>();
        using var store = new SerSessaoOperadorStore(
            NullLogger<SerSessaoOperadorStore>.Instance,
            (usuario, senha) =>
            {
                var s = new SessaoFalsa(usuario, senha) { RecusaLogin = true };
                criadas.Add(s);
                return s;
            });

        var act = async () =>
            await store.AutenticarAsync("jti-a", Operador, "56840827", "errada", default);

        await act.Should().ThrowAsync<ValidacaoException>();
        store.Estado("jti-a").Autenticado.Should().BeFalse();
        criadas.Single().Descartada.Should().BeTrue("senha errada não pode deixar sessão pendurada");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Senha_vazia_nem_chega_a_bater_no_SER(string senha)
    {
        using var store = Novo(out var criadas);

        var act = async () =>
            await store.AutenticarAsync("jti-a", Operador, "56840827", senha, default);

        await act.Should().ThrowAsync<ValidacaoException>();
        criadas.Should().BeEmpty();
    }

    // ------------------------------------------------------------------ dublê

    private sealed class SessaoFalsa(string usuario, string senha) : ISerWebSessao, IDisposable
    {
        public string Usuario { get; } = usuario;
        public string Senha { get; } = senha;
        public bool RecusaLogin { get; init; }
        public bool Descartada { get; private set; }

        /// <summary>É o que o store usa para provar a credencial antes de guardá-la.</summary>
        public Task<string> AbrirTelaPesquisaAsync(CancellationToken cancellationToken) =>
            RecusaLogin
                ? throw new ValidacaoException("ser.credencial_invalida", "Usuário ou senha do SER inválidos.")
                : Task.FromResult("<html><form id=\"form0\"></form></html>");

        public Task<string> AbrirTelaAsync(string caminho, CancellationToken cancellationToken) =>
            AbrirTelaPesquisaAsync(cancellationToken);

        public Task<string> SubmeterPesquisaAsync(
            string htmlForm, IReadOnlyDictionary<string, string> extras, string? viewState,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RespostaSer> SubmeterFormAsync(
            string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
            string? viewState, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<RespostaSer> SubmeterEscritaAsync(
            string htmlPagina, string formId, IReadOnlyDictionary<string, string> extras,
            string? viewState, string operacao, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<string> AutenticarAvulsoAsync(
            string usuario, string senha, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public void UsarCredencialDoOperador(string usuario, string senha) { }

        public void Reiniciar() { }

        public void Dispose() => Descartada = true;
    }
}

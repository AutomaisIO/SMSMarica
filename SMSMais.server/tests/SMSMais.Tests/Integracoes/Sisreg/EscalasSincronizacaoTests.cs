using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Regulacao.Catalogo;
using SMSMais.Core.Regulacao.Catalogo.Dtos;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Core.Integracoes.SisregWeb.Escalas;
using SMSMais.Core.Integracoes.SisregWeb.Escalas.Background;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Sincronismo da grade de escalas ponta a ponta — do CSV ao banco — com a sessão do SISREG
/// dublada. O que está sob teste é a parte que não dá para conferir olhando: o <b>upsert</b>.
///
/// <para><b>Por que upsert e não append:</b> o <c>COD. ESCALA AMBULATORIAL</c> é único no arquivo e
/// o SISREG edita a linha in place — 59% das linhas têm data de alteração diferente da de inserção.
/// Se o sincronismo inserisse uma linha nova a cada mudança, a oferta de quem teve o horário
/// corrigido apareceria duas vezes e a contagem de vagas dobraria sem ninguém notar.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class EscalasSincronizacaoTests(PostgresFixture fixture)
{
    /// <summary>Sessão dublada: devolve o CSV combinado, sem tocar no SISREG.</summary>
    private sealed class SessaoFake(string resposta) : ISisregWebSessao
    {
        /// <summary>Escrita assina com o login do operador — nos dublês não há SISREG para assinar.</summary>
        public void UsarCredencialDoOperador(string usuario, string senha) { }

        public int Chamadas { get; private set; }

        public Task<string> PostFormAsync(
            string caminho, IReadOnlyDictionary<string, string> campos, CancellationToken cancellationToken)
        {
            Chamadas++;
            CamposDoUltimoPost = campos;
            return Task.FromResult(resposta);
        }

        public IReadOnlyDictionary<string, string>? CamposDoUltimoPost { get; private set; }

        public Task<string> GetAsync(
            string caminho, IReadOnlyDictionary<string, string>? query,
            CancellationToken cancellationToken, Func<string, bool>? pareceSessaoCaida = null) =>
            Task.FromResult(resposta);
    }

    private const string Cabecalho =
        "COD. ESCALA AMBULATORIAL;COD. CENTRAL EXEC.;DESC. CENTRAL EXEC.;CPF PROFISSIONAL EXEC.;"
        + "NOME PROFISSIONAL EXEC.;COD. CBO;DESC. CBO;COD CNES EXEC.;DESC. CNES EXEC.;"
        + "COD. PROCEDIMENTO INTERNO;DESC. PROCEDIMENTO INTERNO;COD. PROCEDIMENTO UNIFICADO;"
        + "SIGLA DIA SEMANA;QTD VAGAS PRIM. VEZ;QTD. MINUTOS PRIM. VEZ;QTD. VAGAS RETORNO;"
        + "QTD. MINUTOS RETORNO;QTD. VAGAS RESERVA;QTD. MINUTOS RESERVA;QUEBRA AUTOMATICA;"
        + "AGENDA LOCAL;DATA DE VIGENCIA INICIAL;DATA DE VIGENCIA FINAL;HORA INICIAL;HORA FINAL;"
        + "NOME OPERADOR CRIADOR;NOME OPERADOR MODIFICADOR;DATA ULTIMA ALTERACAO;"
        + "HORA ULTIMA ALTERACAO;STATUS;DATA DA INSERCAO;HORA DA INSERCAO;"
        + "DATA DA ULTIMA ATIVACAO;HORA DA ULTIMA ATIVACAO";

    private static string Linha(string codigo, string cnes, string vagas = "10", string horaFim = "12:00")
    {
        var c = new string[34];
        Array.Fill(c, string.Empty);
        c[0] = codigo;
        c[1] = "330270"; c[2] = "MARICA";
        c[3] = "12345678901"; c[4] = "PROFISSIONAL DE TESTE";
        c[5] = "225320"; c[6] = "MEDICO";
        c[7] = cnes; c[8] = "UNIDADE DE TESTE";
        c[9] = "0229000"; c[10] = "GRUPO - ULTRASSONOGRAFIA"; c[11] = "---";
        c[12] = "SEX";
        c[13] = vagas; c[14] = "0";
        c[15] = "0"; c[16] = "0";
        c[17] = "0"; c[18] = "0";
        c[19] = "SIM"; c[20] = "NAO";
        c[21] = "06/01/2025"; c[22] = "11/09/2027";
        c[23] = "11:10"; c[24] = horaFim;
        c[25] = "OP-CRIADOR"; c[26] = "OP-MODIFICADOR";
        c[27] = "24/08/2026"; c[28] = "12:08";
        c[29] = "ATIVA";
        c[30] = "21/08/2026"; c[31] = "09:08";
        c[32] = "24/08/2026"; c[33] = "12:08";
        return string.Join(';', c);
    }

    private static string Arquivo(params string[] linhas) =>
        Cabecalho + "\n" + string.Join("\n", linhas) + "\n";

    /// <summary>Código de escala novo a cada chamada — a bancada é compartilhada e o índice do
    /// código é único. Só dígitos: o parser recusa qualquer outra coisa, e com razão.</summary>
    private static string Codigo() => Random.Shared.Next(100_000_000, 999_999_999).ToString();

    private static EscalasSincronizacaoService Servico(
        SmsMaisDbContext db, ISisregWebSessao sessao, EscalasSincronizacaoEstadoVivo estado) =>
        new(db,
            sessao,
            new EscalasSincronizacaoFila(),
            estado,
            new SMSMais.Core.Integracoes.SisregWeb.Varredura.Background.VarreduraSisregEstadoVivo(),
            new SMSMais.Core.Integracoes.SisregWeb.Importacao.Background.SisregImportacaoEstadoVivo(),
            new SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Background.MapeamentoLoteEstadoVivo(),
            new SisregOrcamentoRequisicoes(),
            new CredencialFake(),
            new UsuarioAtualAccessorFake(),
            Options.Create(new EscalasSincronizacaoOpcoes()),
            Options.Create(new SisregOrcamentoOpcoes()),
            new CatalogoRegulacaoNulo(),
            NullLogger<EscalasSincronizacaoService>.Instance);

    /// <summary>
    /// O sincronismo de escalas dispara o catálogo canônico da regulação no fim, por carona de
    /// cadência. Aqui isso não é o que está sob teste — e o de verdade precisaria do provedor de
    /// embeddings —, então entra um dublê que não faz nada.
    /// </summary>
    private sealed class CatalogoRegulacaoNulo : IRegulacaoCatalogoService
    {
        public Task<RegulacaoCatalogoSyncResultadoDto> SincronizarAsync(CancellationToken ct) =>
            Task.FromResult(new RegulacaoCatalogoSyncResultadoDto(0, 0, 0, 0, 0, 0));

        public Task<IReadOnlyList<RegulacaoSugestaoPareamentoDto>> ListarSugestoesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RegulacaoSugestaoPareamentoDto>>([]);

        public Task ConfirmarPareamentoAsync(Guid origemId, Guid procedimentoId, CancellationToken ct) =>
            Task.CompletedTask;

        public Task RejeitarPareamentoAsync(Guid origemId, CancellationToken ct) => Task.CompletedTask;

        public Task RenomearCanonicoAsync(Guid procedimentoId, string nome, CancellationToken ct) =>
            Task.CompletedTask;
    }

    /// <summary>Um CNES aleatório por teste: a bancada é compartilhada e o índice de escala é único.</summary>
    private static async Task<(Guid UnidadeId, string Cnes)> CriarUnidadeAsync(SmsMaisDbContext db)
    {
        var cnes = Random.Shared.Next(1_000_000, 9_999_999).ToString();
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNIDADE ESCALA {cnes}",
            Cnes = cnes,
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();
        return (unidade.Id, cnes);
    }

    private static async Task<SisregEscalaSincronizacaoExecucao> ExecutarAsync(
        SmsMaisDbContext db, string csv)
    {
        var estado = new EscalasSincronizacaoEstadoVivo();
        var servico = Servico(db, new SessaoFake(csv), estado);

        var aceita = await servico.IniciarAsync(CancellationToken.None);
        await servico.ExecutarAsync(
            new EscalasSincronizacaoJob(DisparoSincronizacao.Manual, null, null), CancellationToken.None);

        return await db.SisregEscalaSincronizacaoExecucoes.AsNoTracking().SingleAsync(e => e.Id == aceita.ExecucaoId);
    }

    [Fact]
    public async Task Grava_a_escala_nova_e_registra_a_execucao()
    {
        await using var db = fixture.CriarDbContext();
        var (unidadeId, cnes) = await CriarUnidadeAsync(db);
        var codigo = Codigo();

        var execucao = await ExecutarAsync(db, Arquivo(Linha(codigo, cnes)));

        Assert.Equal(StatusVarredura.Concluida, execucao.Status);
        Assert.Equal(1, execucao.EscalasNovas);

        var escala = await db.SisregEscalas.AsNoTracking().SingleAsync(e => e.CodigoEscala == codigo);
        Assert.Equal(unidadeId, escala.UnidadeId);
        Assert.Equal(10, escala.VagasTotal);
        Assert.True(escala.EhGrupo);
        Assert.False(escala.Ausente);
    }

    /// <summary>
    /// O coração do sincronismo: a mesma escala vinda de novo com vagas diferentes tem de
    /// <b>atualizar a linha</b>, não criar outra. Duas linhas para o mesmo código seriam oferta
    /// duplicada — a unidade apareceria com o dobro das vagas que tem.
    /// </summary>
    [Fact]
    public async Task Escala_que_mudou_atualiza_a_linha_em_vez_de_duplicar()
    {
        await using var db = fixture.CriarDbContext();
        var (_, cnes) = await CriarUnidadeAsync(db);
        var codigo = Codigo();

        await ExecutarAsync(db, Arquivo(Linha(codigo, cnes, vagas: "10")));
        var execucao = await ExecutarAsync(db, Arquivo(Linha(codigo, cnes, vagas: "25")));

        Assert.Equal(0, execucao.EscalasNovas);
        Assert.Equal(1, execucao.EscalasAtualizadas);

        var escalas = await db.SisregEscalas.AsNoTracking()
            .Where(e => e.CodigoEscala == codigo).ToListAsync();
        Assert.Single(escalas);
        Assert.Equal(25, escalas[0].VagasTotal);
    }

    /// <summary>Rodar de novo sem mudança nenhuma não pode inflar "atualizadas" — senão o número
    /// perde o sentido e ninguém percebe quando o SISREG realmente mexeu na grade.</summary>
    [Fact]
    public async Task Arquivo_identico_nao_conta_como_atualizacao()
    {
        await using var db = fixture.CriarDbContext();
        var (_, cnes) = await CriarUnidadeAsync(db);
        var codigo = Codigo();
        var csv = Arquivo(Linha(codigo, cnes));

        await ExecutarAsync(db, csv);
        var execucao = await ExecutarAsync(db, csv);

        Assert.Equal(0, execucao.EscalasNovas);
        Assert.Equal(0, execucao.EscalasAtualizadas);
    }

    /// <summary>
    /// Escala que estava ativa aqui e sumiu do arquivo saiu do ar no SISREG: marca ausente em vez de
    /// apagar. O histórico de "esta vaga existia" é o que explica um agendamento antigo que hoje não
    /// teria oferta nenhuma.
    /// </summary>
    [Fact]
    public async Task Escala_que_sumiu_do_arquivo_vira_ausente_sem_ser_apagada()
    {
        await using var db = fixture.CriarDbContext();
        var (_, cnes) = await CriarUnidadeAsync(db);
        var some = Codigo();
        var fica = Codigo();

        await ExecutarAsync(db, Arquivo(Linha(some, cnes), Linha(fica, cnes)));
        await ExecutarAsync(db, Arquivo(Linha(fica, cnes)));

        var sumida = await db.SisregEscalas.AsNoTracking().SingleAsync(e => e.CodigoEscala == some);
        var mantida = await db.SisregEscalas.AsNoTracking().SingleAsync(e => e.CodigoEscala == fica);
        Assert.True(sumida.Ausente);
        Assert.False(mantida.Ausente);
    }

    /// <summary>Voltar a aparecer tem de reabilitar a oferta — senão uma escala reativada no SISREG
    /// ficaria invisível para sempre.</summary>
    [Fact]
    public async Task Escala_que_volta_a_aparecer_deixa_de_ser_ausente()
    {
        await using var db = fixture.CriarDbContext();
        var (_, cnes) = await CriarUnidadeAsync(db);
        var codigo = Codigo();
        var outra = Codigo();

        await ExecutarAsync(db, Arquivo(Linha(codigo, cnes)));
        await ExecutarAsync(db, Arquivo(Linha(outra, cnes)));
        await ExecutarAsync(db, Arquivo(Linha(codigo, cnes), Linha(outra, cnes)));

        var escala = await db.SisregEscalas.AsNoTracking().SingleAsync(e => e.CodigoEscala == codigo);
        Assert.False(escala.Ausente);
    }

    /// <summary>
    /// CNES que não casa com unidade nenhuma não pode entrar: sem <c>unidade_id</c> a oferta não tem
    /// onde pousar. É contado à parte porque zero é o esperado (34 de 34 casaram na medição) e
    /// qualquer valor aí significa unidade nova no SISREG que o catálogo ainda não descobriu.
    /// </summary>
    [Fact]
    public async Task Cnes_sem_unidade_cadastrada_e_contado_e_nao_grava()
    {
        await using var db = fixture.CriarDbContext();
        var codigo = Codigo();

        var execucao = await ExecutarAsync(db, Arquivo(Linha(codigo, cnes: "0000001")));

        Assert.Equal(1, execucao.UnidadesNaoEncontradas);
        Assert.Equal(0, execucao.EscalasNovas);
        Assert.False(await db.SisregEscalas.AnyAsync(e => e.CodigoEscala == codigo));
    }

    /// <summary>
    /// Se o SISREG devolver uma página de erro em vez do CSV, a execução tem de falhar. Sem isto, a
    /// resposta viraria "0 escalas" e o operador leria como "a rede não tem oferta hoje" — e, pior,
    /// o passo seguinte marcaria a grade inteira como ausente.
    /// </summary>
    [Fact]
    public async Task Resposta_que_nao_e_o_arquivo_falha_em_vez_de_zerar_a_oferta()
    {
        await using var db = fixture.CriarDbContext();
        var (_, cnes) = await CriarUnidadeAsync(db);
        var codigo = Codigo();
        await ExecutarAsync(db, Arquivo(Linha(codigo, cnes)));

        var execucao = await ExecutarAsync(db, "<html><body>Erro de Sistema</body></html>");

        Assert.Equal(StatusVarredura.Erro, execucao.Status);
        var escala = await db.SisregEscalas.AsNoTracking().SingleAsync(e => e.CodigoEscala == codigo);
        Assert.False(escala.Ausente);
    }

    /// <summary>
    /// O POST tem de ir com <b>todos</b> os filtros vazios, inclusive o status.
    ///
    /// <para>Filtrar por <c>status=A</c> não economizaria requisição nenhuma (o custo é 1 seja qual
    /// for o recorte) e criaria um erro caro: escala que passasse de ATIVA para EXPIRADA sumiria do
    /// arquivo e seria marcada ausente, ou seja, o sistema diria "essa vaga saiu do SISREG" quando
    /// ela apenas venceu.</para>
    /// </summary>
    [Fact]
    public async Task Pede_ao_sisreg_o_arquivo_inteiro_sem_filtro_de_status()
    {
        await using var db = fixture.CriarDbContext();
        var (_, cnes) = await CriarUnidadeAsync(db);
        var sessao = new SessaoFake(Arquivo(Linha(Codigo(), cnes)));
        var servico = Servico(db, sessao, new EscalasSincronizacaoEstadoVivo());

        await servico.IniciarAsync(CancellationToken.None);
        await servico.ExecutarAsync(
            new EscalasSincronizacaoJob(DisparoSincronizacao.Manual, null, null), CancellationToken.None);

        Assert.Equal(1, sessao.Chamadas);
        Assert.Equal("EXPORTAR_ESCALAS", sessao.CamposDoUltimoPost!["etapa"]);
        // Filtros vazios = a rede inteira E o historico, numa requisicao so.
        Assert.Equal(string.Empty, sessao.CamposDoUltimoPost["status"]);
        Assert.Equal(string.Empty, sessao.CamposDoUltimoPost["ups"]);
        Assert.Equal(string.Empty, sessao.CamposDoUltimoPost["cpf"]);
    }

    [Theory]
    [InlineData("2:30")]
    [InlineData("25:00")]
    [InlineData("abc")]
    [InlineData(null)]
    public void Hora_invalida_no_agendamento_e_recusada(string? hora)
    {
        Assert.Throws<ValidacaoException>(() => EscalasSincronizacaoService.NormalizarHora(hora));
    }

    [Fact]
    public void Hora_valida_e_normalizada() =>
        Assert.Equal("02:30", EscalasSincronizacaoService.NormalizarHora(" 02:30 "));
}

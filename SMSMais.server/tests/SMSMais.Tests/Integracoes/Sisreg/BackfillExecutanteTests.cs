using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.Integracoes.SisregWeb.Importacao;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// Recuperação do profissional EXECUTANTE das solicitações já importadas, relendo
/// <c>Solicitacao.RawSisreg</c>.
///
/// <para><b>Por que este backfill existe:</b> o <c>AgendaTxtParser</c> sempre leu o CPF (coluna 4) e
/// o nome (coluna 5) do executante para montar o mapeamento de profissionais, mas o valor nunca era
/// gravado na solicitação — e sem ele não existe "agenda do profissional", só agregado por unidade.
/// Como a linha crua ficou guardada desde o começo, o passado inteiro é recuperável **sem falar com
/// o SISREG**: nenhuma requisição, nenhum risco de CAPTCHA, nenhuma disputa pela sessão única.</para>
///
/// <para>Os riscos que estes testes prendem giram todos em torno da mesma sutileza: a consulta
/// filtra por "executante ainda nulo", então quem é preenchido sai do resultado sozinho, mas quem
/// NÃO pôde ser preenchido continua nele e voltaria no próximo lote. Daí (a) o laço não pode girar
/// para sempre; (b) uma linha ruim não pode esconder as boas que vêm depois dela na ordem; e
/// (c) rodar de novo não pode reescrever nem apagar o que já está certo.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class BackfillExecutanteTests(PostgresFixture fixture)
{
    private const string CpfExecutante = "12345678901";
    private const string NomeExecutante = "PROFISSIONAL DE TESTE";

    /// <summary>
    /// Uma linha do layout do SISREG: 38 campos, 1ª coluna só dígitos, executante nas colunas 4 e 5.
    /// Montada campo a campo em vez de colada de produção — a linha real carrega PII de paciente.
    /// </summary>
    private static string LinhaTxt(string codigoSolicitacao, string cpfExecutante = CpfExecutante)
    {
        var c = new string[38];
        Array.Fill(c, string.Empty);
        c[0] = codigoSolicitacao;
        c[1] = "0229000";
        c[3] = "PROCEDIMENTO DE TESTE";
        c[4] = cpfExecutante;
        c[5] = NomeExecutante;
        c[6] = "10.09.2026";
        c[7] = "08:00";
        return string.Join(';', c);
    }

    private static BackfillExecutanteService Servico(SmsMaisDbContext db, int tamanhoLote = 500) =>
        new(db, NullLogger<BackfillExecutanteService>.Instance) { TamanhoLote = tamanhoLote };

    private static async Task<Guid> SemearAsync(SmsMaisDbContext db, string? raw)
    {
        // `unidade_executante_id` é FK obrigatória — sem uma unidade de verdade o insert nem chega
        // ao que este teste quer exercitar.
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNIDADE BACKFILL {Guid.NewGuid():N}"[..40],
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);

        var solicitacao = new Solicitacao
        {
            Id = Guid.CreateVersion7(),
            PacienteId = Guid.NewGuid(),
            Categoria = CategoriaSolicitacao.Imagem,
            UnidadeExecutanteId = unidade.Id,
            SolicitanteNome = "NÃO INFORMADO",
            SolicitanteNumConselho = string.Empty,
            SolicitanteUfConselho = string.Empty,
            SolicitanteConselho = "CRM",
            Status = StatusSolicitacao.Solicitada,
            Prioridade = PrioridadeSolicitacao.Eletiva,
            RawSisreg = raw,
            CriadoEm = DateTime.UtcNow,
        };
        db.Solicitacoes.Add(solicitacao);
        await db.SaveChangesAsync();
        return solicitacao.Id;
    }

    [Fact]
    public async Task Preenche_o_executante_a_partir_da_linha_crua()
    {
        await using var db = fixture.CriarDbContext();
        var id = await SemearAsync(db, LinhaTxt("9001001"));

        await Servico(db).ExecutarAsync(CancellationToken.None);

        var alvo = await db.Solicitacoes.AsNoTracking().SingleAsync(s => s.Id == id);
        Assert.Equal(CpfExecutante, alvo.ProfissionalExecutanteCpf);
        Assert.Equal(NomeExecutante, alvo.ProfissionalExecutanteNome);
    }

    /// <summary>
    /// O RAW do caminho pontual do <c>cons_agendas</c> é JSON e não carrega executante (lá o
    /// profissional era o filtro da consulta, não uma coluna do resultado). Tem de ser contado como
    /// ignorado — e, sobretudo, <b>não pode travar o laço</b>: como a consulta filtra por "executante
    /// ainda nulo", a linha continua batendo no filtro e voltaria para sempre se não fosse pulada.
    /// </summary>
    [Fact]
    public async Task Raw_em_json_nao_tem_executante_e_nao_trava_o_laco()
    {
        await using var db = fixture.CriarDbContext();
        var id = await SemearAsync(db, """{"origem":"cons_agendas","codigo":"9002001"}""");

        var resultado = await Servico(db).ExecutarAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(30));

        var alvo = await db.Solicitacoes.AsNoTracking().SingleAsync(s => s.Id == id);
        Assert.Null(alvo.ProfissionalExecutanteCpf);
        Assert.True(resultado.SemDadoNoRaw >= 1);
    }

    /// <summary>Linha fora do layout não pode derrubar o backfill — só continua pendente.</summary>
    [Fact]
    public async Task Linha_fora_do_layout_e_ignorada_sem_derrubar()
    {
        await using var db = fixture.CriarDbContext();
        var id = await SemearAsync(db, "isto;nao;e;uma;linha;do;sisreg");

        await Servico(db).ExecutarAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

        var alvo = await db.Solicitacoes.AsNoTracking().SingleAsync(s => s.Id == id);
        Assert.Null(alvo.ProfissionalExecutanteCpf);
    }

    /// <summary>
    /// Rodar de novo é seguro: a segunda passada não examina quem já está preenchido, então não
    /// reescreve nem apaga. É o que permite ao operador clicar sem medo quando algo falhar no meio.
    /// </summary>
    [Fact]
    public async Task Rodar_de_novo_nao_mexe_no_que_ja_esta_preenchido()
    {
        await using var db = fixture.CriarDbContext();
        var id = await SemearAsync(db, LinhaTxt("9003001"));

        await Servico(db).ExecutarAsync(CancellationToken.None);
        var segunda = await Servico(db).ExecutarAsync(CancellationToken.None);

        var alvo = await db.Solicitacoes.AsNoTracking().SingleAsync(s => s.Id == id);
        Assert.Equal(CpfExecutante, alvo.ProfissionalExecutanteCpf);
        Assert.Equal(0, segunda.Preenchidas);
    }

    /// <summary>
    /// CPF que não tem 11 dígitos não vira executante meia-boca: o campo continua nulo. Preencher
    /// com lixo seria pior que vazio — a agenda passaria a agrupar por um identificador inválido.
    /// </summary>
    [Fact]
    public async Task Cpf_invalido_na_linha_nao_preenche()
    {
        await using var db = fixture.CriarDbContext();
        var id = await SemearAsync(db, LinhaTxt("9004001", cpfExecutante: "000"));

        await Servico(db).ExecutarAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(30));

        var alvo = await db.Solicitacoes.AsNoTracking().SingleAsync(s => s.Id == id);
        Assert.Null(alvo.ProfissionalExecutanteCpf);
    }

    /// <summary>
    /// <b>Regressão.</b> Uma linha sem executante não pode esconder as boas que vêm depois dela.
    ///
    /// <para>A consulta do backfill filtra por "executante ainda nulo": quem é preenchido sai do
    /// resultado sozinho, mas quem NÃO pôde ser preenchido continua nele e volta no próximo lote. A
    /// primeira versão deste serviço parava o laço quando um lote inteiro vinha sem nada a preencher
    /// — o que, com a linha ruim vindo primeiro na ordem, abortaria o backfill inteiro na primeira
    /// página e deixaria milhares de solicitações para trás, sem erro nenhum na tela.</para>
    ///
    /// <para>Lote de 1 para forçar a página ruim a vir sozinha, que é exatamente o caso degenerado.</para>
    /// </summary>
    [Fact]
    public async Task Linha_sem_executante_nao_esconde_as_boas_que_vem_depois()
    {
        await using var db = fixture.CriarDbContext();

        // Guid v7 é ordenável por criação: semear nesta ordem garante que a ruim venha primeiro.
        await SemearAsync(db, """{"origem":"cons_agendas"}""");
        var boa = await SemearAsync(db, LinhaTxt("9005001"));

        await Servico(db, tamanhoLote: 1).ExecutarAsync(CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(60));

        var alvo = await db.Solicitacoes.AsNoTracking().SingleAsync(s => s.Id == boa);
        Assert.Equal(CpfExecutante, alvo.ProfissionalExecutanteCpf);
    }
}

using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Tests.Infraestrutura;
using Tipo = SMSMais.Data.Entities.Enums.TipoEventoAtendimentoConfirmacao;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// Aba Equipe das Confirmações: a produção por atendente sai da trilha, e o que ela protege é a
/// LEITURA da trilha — cada ato vai para quem o fez (não para quem tinha a ficha), o tempo até o
/// desfecho parte do momento em que a ficha chegou à mão de quem resolveu (inclusive por
/// transferência), pausa longa não vira ritmo, e ato do sistema não é produção de ninguém.
///
/// <para>A bancada acumula linhas entre execuções: os asserts olham só as atendentes criadas aqui,
/// nunca o total.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ConfirmacoesEquipeTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Cada_ato_vai_para_quem_fez_e_o_tempo_conta_da_transferencia()
    {
        await using var db = fixture.CriarDbContext();
        var ana = await CriarUsuarioAsync(db, "ANA");
        var bia = await CriarUsuarioAsync(db, "BIA");
        // Ontem às 9h de Brasília: fixo no dia, para a virada da meia-noite não partir o "mesmo dia"
        // do ritmo conforme a hora em que a suíte roda.
        var hoje = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        var t0 = FusoBrasilia.DeBrasiliaParaUtc(hoje.AddDays(-1).ToDateTime(new TimeOnly(9, 0)));

        // Ficha 1: Ana pega e confirma em 4 min.
        var f1 = await CriarAtendimentoAsync(db, ana);
        Evento(db, f1, Tipo.Atendido, ana, t0);
        Evento(db, f1, Tipo.Confirmado, ana, t0.AddMinutes(4));

        // Ficha 2: Ana pega, transfere para Bia aos 10 min, Bia cancela 6 min depois — aqui e no SISREG.
        var f2 = await CriarAtendimentoAsync(db, ana);
        Evento(db, f2, Tipo.Atendido, ana, t0.AddMinutes(5));
        Evento(db, f2, Tipo.Transferido, ana, t0.AddMinutes(10), de: ana, para: bia);
        Evento(db, f2, Tipo.Cancelado, bia, t0.AddMinutes(16));
        Evento(db, f2, Tipo.CanceladoNoSisreg, bia, t0.AddMinutes(16));
        Evento(db, f2, Tipo.PacienteAvisadoCancelamento, bia, t0.AddMinutes(16));

        // Ficha 3: Ana marca telefone errado aos 14 min (10 min depois do 1º desfecho) e, depois
        // de uma pausa de 2h, estaciona outra — a pausa não pode virar ritmo.
        var f3 = await CriarAtendimentoAsync(db, ana);
        Evento(db, f3, Tipo.Atendido, ana, t0.AddMinutes(12));
        Evento(db, f3, Tipo.ContatoErrado, ana, t0.AddMinutes(14));
        var f4 = await CriarAtendimentoAsync(db, ana);
        Evento(db, f4, Tipo.Atendido, ana, t0.AddMinutes(133));
        Evento(db, f4, Tipo.EnviadoPendente, ana, t0.AddMinutes(134));

        // Ato do sistema (sem autor): não é produção de ninguém.
        Evento(db, f4, Tipo.Liberado, null, t0.AddMinutes(135));
        await db.SaveChangesAsync();

        var equipe = await new ConfirmacoesEquipeService(db).ObterAsync(hoje.AddDays(-1), hoje);

        var a = Assert.Single(equipe.Atendentes, x => x.UsuarioId == ana);
        Assert.Equal(4, a.Pegou);
        Assert.Equal(1, a.Confirmou);
        Assert.Equal(0, a.Cancelou);
        Assert.Equal(1, a.Transferiu);
        Assert.Equal(1, a.ContatoErrado);
        Assert.Equal(1, a.Pendente);
        Assert.Equal(0, a.Liberou);
        Assert.Equal(3, a.Desfechos);
        // Pegar→desfecho: 4, 2 e 1 min → mediana 2.
        Assert.Equal(2, a.TempoAteDesfechoMin);
        // Entre desfechos: 10 min e 120 min; o de 120 é pausa → sobra só o de 10.
        Assert.Equal(10, a.RitmoMin);

        var b = Assert.Single(equipe.Atendentes, x => x.UsuarioId == bia);
        Assert.Equal(0, b.Pegou);
        Assert.Equal(1, b.Cancelou);
        Assert.Equal(1, b.CancelouNoSisreg);
        Assert.Equal(1, b.AvisouPaciente);
        Assert.Equal(1, b.Desfechos);
        // A ficha chegou à mão da Bia na transferência (10 min), não quando a Ana pegou (5 min).
        Assert.Equal(6, b.TempoAteDesfechoMin);
        Assert.Null(b.RitmoMin);

        var atos = await new ConfirmacoesEquipeService(db).AtosAsync(bia, hoje.AddDays(-1), hoje);
        Assert.Equal(3, atos.Count);
        Assert.All(atos, x => Assert.NotEqual(Guid.Empty, x.SolicitacaoId));
    }

    [Fact]
    public async Task Fora_do_periodo_nao_conta()
    {
        await using var db = fixture.CriarDbContext();
        var ana = await CriarUsuarioAsync(db, "ANA");
        var f = await CriarAtendimentoAsync(db, ana);
        Evento(db, f, Tipo.Atendido, ana, DateTime.UtcNow.AddDays(-40));
        Evento(db, f, Tipo.Confirmado, ana, DateTime.UtcNow.AddDays(-40).AddMinutes(3));
        await db.SaveChangesAsync();

        var hoje = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        var equipe = await new ConfirmacoesEquipeService(db).ObterAsync(hoje.AddDays(-6), hoje);

        Assert.DoesNotContain(equipe.Atendentes, x => x.UsuarioId == ana);
    }

    // ===================== apoio =====================

    private static async Task<Guid> CriarUsuarioAsync(SmsMaisDbContext db, string nome)
    {
        var u = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = $"{nome} {Guid.NewGuid():N}"[..20],
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(u);
        await db.SaveChangesAsync();
        return u.Id;
    }

    /// <summary>Atendimento já ENCERRADO: o índice único de "ativo por solicitação" não entra na história.</summary>
    private static async Task<AtendimentoConfirmacao> CriarAtendimentoAsync(SmsMaisDbContext db, Guid atendente)
    {
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid(), dataAgendada: DateTime.UtcNow.AddDays(2));
        var a = new AtendimentoConfirmacao
        {
            Id = Guid.CreateVersion7(),
            SolicitacaoId = exame.SolicitacaoId,
            AtendenteUsuarioId = atendente,
            Situacao = SituacaoAtendimentoConfirmacao.Liberado,
            IniciadoEm = DateTime.UtcNow.AddHours(-3),
            EncerradoEm = DateTime.UtcNow,
            CriadoPor = atendente,
        };
        db.AtendimentosConfirmacao.Add(a);
        await db.SaveChangesAsync();
        return a;
    }

    private static void Evento(
        SmsMaisDbContext db, AtendimentoConfirmacao a, Tipo tipo, Guid? ator, DateTime quando,
        Guid? de = null, Guid? para = null) =>
        db.AtendimentoConfirmacaoEventos.Add(new AtendimentoConfirmacaoEvento
        {
            Id = Guid.CreateVersion7(),
            AtendimentoId = a.Id,
            Tipo = tipo,
            AtorUsuarioId = ator,
            DeUsuarioId = de,
            ParaUsuarioId = para,
            OcorridoEm = quando,
        });
}

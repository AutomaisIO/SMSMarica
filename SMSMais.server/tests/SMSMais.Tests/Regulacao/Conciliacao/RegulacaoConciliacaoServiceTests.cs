using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using SMSMais.Core.Regulacao.Conciliacao;
using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Regulacao.Conciliacao;

/// <summary>
/// O encontro entre a solicitação aberta aqui e o registro que existe no sistema de regulação
/// (plano 05).
///
/// <para>O que prendem: o vínculo se faz <b>pelo número externo</b> e nada mais; a situação de lá
/// vira estado nosso com evento na trilha; e a varredura, que relê os mesmos casos todo dia,
/// <b>não gera evento quando nada mudou</b> — senão a linha do tempo vira um diário de leituras.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoConciliacaoServiceTests(PostgresFixture fixture)
{
    private static string Sufixo() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static RegulacaoConciliacaoService Servico(SmsMaisDbContext db)
    {
        // Sem usuário: a conciliação roda como sistema, e é isso que faz os eventos saírem com
        // papel `Sistema` — a trilha distingue "a varredura trouxe" de "alguém mudou".
        var acessor = new UsuarioAtualAccessorFake(null, null);
        return new RegulacaoConciliacaoService(
            db, new RegulacaoEventoService(db, acessor),
            NullLogger<RegulacaoConciliacaoService>.Instance);
    }

    private static async Task<(Guid SolicitacaoId, string Numero, Guid EspelhoId)> CenarioSerAsync(
        SmsMaisDbContext db, SituacaoSer situacaoNoSer)
    {
        var sufixo = Sufixo();
        var unidade = new Unidade { Id = Guid.NewGuid(), Nome = $"UNID CONC {sufixo}", CriadoEm = DateTime.UtcNow };
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = "AUTOR CONC",
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        var procedimento = new RegulacaoProcedimento
        {
            Id = Guid.CreateVersion7(),
            NomeCanonico = $"PROC CONC {sufixo}",
            NomeNormalizado = "PROC CONC",
            Tipo = TipoProcedimentoRegulacao.Consulta,
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        db.Usuarios.Add(usuario);
        db.RegulacaoProcedimentos.Add(procedimento);
        await db.SaveChangesAsync();

        var numero = $"SER{sufixo}";
        var espelho = new SerSolicitacao
        {
            Id = Guid.NewGuid(),
            IdSer = numero,
            Situacao = situacaoNoSer,
            CriadoEm = DateTime.UtcNow,
        };
        db.SerSolicitacoes.Add(espelho);

        var solicitacao = new RegulacaoSolicitacao
        {
            Id = Guid.CreateVersion7(),
            Fluxo = FluxoRegulacao.Externo,
            UnidadeSolicitanteId = unidade.Id,
            CriadoPorUsuarioId = usuario.Id,
            PacienteId = Guid.NewGuid(),
            PacienteNome = "PACIENTE CONC",
            ProcedimentoId = procedimento.Id,
            SistemaDestino = SistemaRegulacao.Ser,
            NumeroExterno = numero,
            FormularioJson = """{"canonico":{}}""",
            Status = StatusRegulacao.EnviadaAoSistema,
            EnviadoEm = DateTime.UtcNow,
            CriadoEm = DateTime.UtcNow,
        };
        db.RegulacaoSolicitacoes.Add(solicitacao);
        await db.SaveChangesAsync();

        // O change tracker guardaria o estado de agora e mascararia o efeito da conciliação.
        db.ChangeTracker.Clear();

        return (solicitacao.Id, numero, espelho.Id);
    }

    [Fact]
    public async Task Casa_pelo_numero_externo_e_traz_a_situacao_de_la()
    {
        await using var db = fixture.CriarDbContext();
        var (solicitacaoId, numero, espelhoId) = await CenarioSerAsync(db, SituacaoSer.Agendada);

        var mudadas = await Servico(db).ConciliarSerAsync([numero], CancellationToken.None);

        mudadas.Should().Be(1);

        var depois = await db.RegulacaoSolicitacoes.AsNoTracking().FirstAsync(x => x.Id == solicitacaoId);
        depois.SerSolicitacaoId.Should().Be(espelhoId, "a FK é o vínculo permanente com o espelho");
        depois.Status.Should().Be(StatusRegulacao.Agendada);

        var evento = await db.RegulacaoEventos.AsNoTracking()
            .FirstAsync(e => e.SolicitacaoId == solicitacaoId && e.Tipo == TipoEventoRegulacao.SituacaoExterna);
        evento.Papel.Should().Be(PapelEventoRegulacao.Sistema, "não foi uma pessoa que mudou");
        evento.StatusAnterior.Should().Be(StatusRegulacao.EnviadaAoSistema);
        evento.StatusNovo.Should().Be(StatusRegulacao.Agendada);
    }

    [Fact]
    public async Task Reler_o_mesmo_caso_no_dia_seguinte_nao_gera_evento_novo()
    {
        await using var db = fixture.CriarDbContext();
        var (solicitacaoId, numero, _) = await CenarioSerAsync(db, SituacaoSer.Agendada);

        await Servico(db).ConciliarSerAsync([numero], CancellationToken.None);
        db.ChangeTracker.Clear();
        var naSegunda = await Servico(db).ConciliarSerAsync([numero], CancellationToken.None);

        // A varredura passa todo dia pelos mesmos casos. Sem esta guarda, a linha do tempo viraria
        // um diário de leituras em vez da história do caso.
        naSegunda.Should().Be(0);
        (await db.RegulacaoEventos.AsNoTracking()
            .CountAsync(e => e.SolicitacaoId == solicitacaoId && e.Tipo == TipoEventoRegulacao.SituacaoExterna))
            .Should().Be(1);
    }

    [Fact]
    public async Task Numero_que_nao_e_nosso_e_ignorado_sem_erro()
    {
        await using var db = fixture.CriarDbContext();
        await CenarioSerAsync(db, SituacaoSer.EmFila);

        // A varredura traz milhares de números do SER; a esmagadora maioria não nasceu aqui.
        var mudadas = await Servico(db).ConciliarSerAsync(
            [$"NAOEXISTE{Sufixo()}"], CancellationToken.None);

        mudadas.Should().Be(0);
    }

    [Fact]
    public async Task Um_caso_ja_concluido_nao_regride_pela_varredura()
    {
        await using var db = fixture.CriarDbContext();
        var (solicitacaoId, numero, _) = await CenarioSerAsync(db, SituacaoSer.EmFila);

        var linha = await db.RegulacaoSolicitacoes.FirstAsync(x => x.Id == solicitacaoId);
        linha.Status = StatusRegulacao.Concluida;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        await Servico(db).ConciliarSerAsync([numero], CancellationToken.None);

        // Quem segura é a máquina: de Concluída não sai transição nenhuma.
        (await db.RegulacaoSolicitacoes.AsNoTracking().FirstAsync(x => x.Id == solicitacaoId))
            .Status.Should().Be(StatusRegulacao.Concluida);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Exames;
using SMSMais.Core.Exames.ImagensInvalidadas;
using SMSMais.Core.Identidade;
using SMSMais.Core.Identidade.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Exames;

/// <summary>
/// APAGA-IMAGEM-INVALIDADA (20/10/2026): a médica retira do que vai ao paciente as imagens ruins
/// que o aparelho mandou junto. Regras do Rafael: retirar — quem lauda ou acesso global (também
/// depois de assinado); devolver — quem retirou ou acesso global; o arquivo do paciente é refeito
/// na hora.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ImagensInvalidadasServiceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Quem_lauda_retira_a_imagem_e_o_pdf_do_paciente_e_refeito()
    {
        await using var db = fixture.CriarDbContext();
        var medica = await SemearUsuarioAsync(db, acessoGlobal: false);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var (servico, pdf) = Criar(db, laudam: [medica], instanciasNoPacs: 5);

        var dto = await servico.InvalidarAsync(exame.StudyInstanceUID, "1.2.3.4", medica, "tremida");

        var imagem = Assert.Single(dto.Imagens);
        Assert.Equal("1.2.3.4", imagem.SopInstanceUID);
        Assert.Equal("tremida", imagem.Motivo);
        Assert.True(imagem.PodeRestaurar); // foi ela quem retirou
        Assert.True(dto.PodeInvalidar);
        // O arquivo do paciente: caches apagados e PDF regerado já com o filtro.
        await pdf.Received(1).InvalidarAsync(exame.Id, Arg.Any<CancellationToken>());
        await pdf.Received(1).GerarOuObterAsync(exame.Id, Arg.Any<CancellationToken>());
        Assert.Equal(["1.2.3.4"], await SopsInvalidados.DoEstudoAsync(db, exame.StudyInstanceUID, default));
    }

    [Fact]
    public async Task Quem_nao_lauda_nem_tem_acesso_global_nao_retira()
    {
        await using var db = fixture.CriarDbContext();
        var recepcao = await SemearUsuarioAsync(db, acessoGlobal: false);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var (servico, _) = Criar(db, laudam: [], instanciasNoPacs: 5);

        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            servico.InvalidarAsync(exame.StudyInstanceUID, "1.2.3.4", recepcao, null));
        Assert.Contains("lauda", ex.Message);
        Assert.Empty(await SopsInvalidados.DoEstudoAsync(db, exame.StudyInstanceUID, default));
    }

    [Fact]
    public async Task Acesso_global_retira_mesmo_sem_permissao_de_laudo()
    {
        await using var db = fixture.CriarDbContext();
        var admin = await SemearUsuarioAsync(db, acessoGlobal: true);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var (servico, _) = Criar(db, laudam: [], instanciasNoPacs: 5);

        var dto = await servico.InvalidarAsync(exame.StudyInstanceUID, "1.2.3.4", admin, null);

        Assert.Single(dto.Imagens);
    }

    [Fact]
    public async Task Retirar_de_novo_a_mesma_imagem_nao_duplica()
    {
        await using var db = fixture.CriarDbContext();
        var medica = await SemearUsuarioAsync(db, acessoGlobal: false);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var (servico, _) = Criar(db, laudam: [medica], instanciasNoPacs: 5);

        await servico.InvalidarAsync(exame.StudyInstanceUID, "1.2.3.4", medica, null);
        var dto = await servico.InvalidarAsync(exame.StudyInstanceUID, "1.2.3.4", medica, null);

        Assert.Single(dto.Imagens);
        Assert.Equal(1, await db.ImagensInvalidadas.CountAsync(i => i.StudyInstanceUID == exame.StudyInstanceUID));
    }

    [Fact]
    public async Task Nao_deixa_retirar_a_ultima_imagem_do_paciente()
    {
        await using var db = fixture.CriarDbContext();
        var medica = await SemearUsuarioAsync(db, acessoGlobal: false);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var (servico, _) = Criar(db, laudam: [medica], instanciasNoPacs: 2);

        await servico.InvalidarAsync(exame.StudyInstanceUID, "1.1", medica, null);
        await Assert.ThrowsAsync<ConflitoException>(() =>
            servico.InvalidarAsync(exame.StudyInstanceUID, "1.2", medica, null));
    }

    [Fact]
    public async Task So_quem_retirou_ou_acesso_global_devolve()
    {
        await using var db = fixture.CriarDbContext();
        var medica = await SemearUsuarioAsync(db, acessoGlobal: false);
        var outraMedica = await SemearUsuarioAsync(db, acessoGlobal: false);
        var admin = await SemearUsuarioAsync(db, acessoGlobal: true);
        var exame = await SeedSolicitacao.CriarAsync(db, Guid.NewGuid());
        var (servico, _) = Criar(db, laudam: [medica, outraMedica], instanciasNoPacs: 5);

        await servico.InvalidarAsync(exame.StudyInstanceUID, "1.2.3.4", medica, null);

        // Outra médica vê a imagem retirada, mas não pode devolver.
        var visaoOutra = await servico.ListarAsync(exame.StudyInstanceUID, outraMedica);
        Assert.False(Assert.Single(visaoOutra.Imagens).PodeRestaurar);
        await Assert.ThrowsAsync<ConflitoException>(() =>
            servico.RestaurarAsync(exame.StudyInstanceUID, "1.2.3.4", outraMedica));

        // Acesso global devolve; a linha fica como trilha (soft delete), e a imagem volta ao PDF.
        var dto = await servico.RestaurarAsync(exame.StudyInstanceUID, "1.2.3.4", admin);
        Assert.Empty(dto.Imagens);
        Assert.Empty(await SopsInvalidados.DoEstudoAsync(db, exame.StudyInstanceUID, default));
        var trilha = await db.ImagensInvalidadas.AsNoTracking().SingleAsync(i => i.StudyInstanceUID == exame.StudyInstanceUID);
        Assert.Equal(admin, trilha.ExcluidoPor);
    }

    // ---- apoio ----

    private static (ImagensInvalidadasService Servico, IExameImagensPdfService Pdf) Criar(
        SmsMaisDbContext db, Guid[] laudam, int instanciasNoPacs)
    {
        var identidade = Substitute.For<IIdentidadeService>();
        identidade.ObterPermissoesResolvidasAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(c =>
            {
                IReadOnlyList<PermissaoModuloDto> resolvidas = laudam.Contains(c.ArgAt<Guid>(0))
                    ? [new PermissaoModuloDto(ModuloPermissao.Laudos, AcoesPermissao.Consulta | AcoesPermissao.Edicao)]
                    : [new PermissaoModuloDto(ModuloPermissao.Pacs, AcoesPermissao.Consulta)];
                return new PermissoesResolvidasDto([], [], resolvidas);
            });

        var reader = Substitute.For<IExamePacsImagensReader>();
        reader.ContarInstanciasPacsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(instanciasNoPacs);

        var pdf = Substitute.For<IExameImagensPdfService>();
        var servico = new ImagensInvalidadasService(
            db, identidade, reader, pdf, NullLogger<ImagensInvalidadasService>.Instance);
        return (servico, pdf);
    }

    private static async Task<Guid> SemearUsuarioAsync(SmsMaisDbContext db, bool acessoGlobal)
    {
        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = $"USUARIO TESTE {Guid.NewGuid():N}"[..24],
            Email = $"{Guid.NewGuid():N}@teste.local",
            Cpf = SeedSolicitacao.CpfAleatorio(),
            SenhaHash = "x",
            Ativo = true,
            AcessoGlobal = acessoGlobal,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return usuario.Id;
    }
}

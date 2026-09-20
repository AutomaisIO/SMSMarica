using FluentAssertions;
using SMSMais.Core.Notificacoes.WhatsApp;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// Modelo com FOTO no topo exige o componente de cabeçalho em cada envio — a arte do modelo
/// aprovado é só exemplo. Estes testes cobrem o que o sistema precisa saber antes de enviar:
/// ler o formato do cabeçalho do catálogo e a arte que a plataforma escolheu para o modelo.
///
/// <para>Sem banco e sem rede: é leitura do JSON do catálogo.</para>
/// </summary>
public sealed class CabecalhoModeloWhatsAppTests
{
    /// <summary>O catálogo do Automais.Zap (<c>GET /v1/templates</c>) com os três casos.</summary>
    private const string Catalogo = """
        {
          "templates": [
            {
              "nome": "confirmacao_exame", "idioma": "pt_BR", "categoria": "UTILITY",
              "corpo": "Olá {{1}}, seu exame foi agendado!",
              "parametros": 1, "exemplos": ["Sr. João"],
              "cabecalho": {
                "formato": "IMAGE", "texto": null, "parametros": 0, "exige_midia": true,
                "exemplo": "https://scontent.whatsapp.net/v/exemplo.png"
              }
            },
            {
              "nome": "laudo_disponivel", "idioma": "pt_BR", "categoria": "UTILITY",
              "corpo": "{{1}}, o laudo do seu {{2}} de {{3}} está pronto.",
              "parametros": 3, "exemplos": []
            },
            {
              "nome": "aviso_unidade", "idioma": "pt_BR", "categoria": "UTILITY",
              "corpo": "Sua unidade estará fechada.", "parametros": 0, "exemplos": [],
              "cabecalho": { "formato": "TEXT", "texto": "Aviso da {{1}}", "parametros": 1 }
            }
          ]
        }
        """;

    [Fact]
    public void Catalogo_diz_que_o_modelo_tem_foto_no_cabecalho()
    {
        var modelos = WhatsAppCliente.ParsearTemplates(Catalogo);

        var exame = modelos.Single(m => m.Nome == "confirmacao_exame");
        exame.Cabecalho.Should().NotBeNull();
        exame.Cabecalho!.Formato.Should().Be("IMAGE");
        exame.Cabecalho.ExigeMidia.Should().BeTrue("a arte precisa ir em TODA mensagem");
        exame.Cabecalho.TipoMidia.Should().Be("image", "é o nome do parâmetro na Cloud API");

        // O corpo continua sendo lido como antes.
        exame.Parametros.Should().Be(1);
        exame.Exemplos.Should().ContainSingle();
    }

    [Fact]
    public void Modelo_sem_cabecalho_nao_ganha_componente()
    {
        var modelos = WhatsAppCliente.ParsearTemplates(Catalogo);

        // Mandar header num modelo que não tem é o mesmo 132012, ao contrário.
        modelos.Single(m => m.Nome == "laudo_disponivel").Cabecalho.Should().BeNull();
    }

    [Fact]
    public void Cabecalho_de_texto_com_variavel_e_reconhecido_como_tal()
    {
        var aviso = WhatsAppCliente.ParsearTemplates(Catalogo).Single(m => m.Nome == "aviso_unidade").Cabecalho!;

        aviso.Formato.Should().Be("TEXT");
        aviso.ExigeMidia.Should().BeFalse("texto não pede arquivo");
        aviso.Parametros.Should().Be(1);
    }

    [Fact]
    public void Catalogo_de_relay_antigo_nao_quebra_a_leitura()
    {
        // Enquanto o relay não for atualizado, o campo simplesmente não vem — e o envio precisa
        // continuar caindo no mapa configurado, não parar.
        const string semCampo = """
            {"templates":[{"nome":"confirmacao_exame","idioma":"pt_BR","categoria":"UTILITY",
            "corpo":"Olá {{1}}","parametros":1,"exemplos":[]}]}
            """;

        var modelos = WhatsAppCliente.ParsearTemplates(semCampo);

        modelos.Should().ContainSingle();
        modelos[0].Cabecalho.Should().BeNull();
        modelos[0].Parametros.Should().Be(1);
    }

    [Fact]
    public void Catalogo_traz_a_arte_escolhida_na_plataforma()
    {
        // A imagem é gerida no Automais.Zap: o catálogo entrega a URL pronta e a instância não
        // precisa saber onde o arquivo mora.
        const string comArte = """
            {"templates":[{"nome":"confirmacao_exame","idioma":"pt_BR","categoria":"UTILITY",
            "corpo":"Olá {{1}}","parametros":1,"exemplos":[],
            "cabecalho":{"formato":"IMAGE","exige_midia":true,
            "arte":"https://zap.automais.io/midias/0198f0c2d4e57b3a9c1e2f3a4b5c6d7e"}}]}
            """;

        var cabecalho = WhatsAppCliente.ParsearTemplates(comArte)[0].Cabecalho!;

        cabecalho.Arte.Should().Be("https://zap.automais.io/midias/0198f0c2d4e57b3a9c1e2f3a4b5c6d7e");
        cabecalho.ExigeMidia.Should().BeTrue();
    }
}

import { Bus } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { BotaoRef } from '@/features/manual/components/Referencia';
import { SimulacaoDesenhoVeiculo } from '@/features/manual/simulacoes/SimulacaoDesenhoVeiculo';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo do cadastro de Veículos: dados, cor com paleta, desenho do modelo e layout de assentos.
 *
 * Conferido no código: `features/veiculos` (VeiculosPage, VeiculoDetalhePage, FormularioVeiculo,
 * MapaDeAssentos, SeletorCorVeiculo, IlustracaoVeiculo, lib/corVeiculo, lib/desenhoVeiculo),
 * `features/translados` (TransladoDetalhePage, MapaDeAssentosAlocavel) e no backend
 * (`VeiculosService` — placa única e exclusão lógica —, `VeiculosValidators`, `VeiculosController`).
 */
export const artigoVeiculos: Artigo = {
  slug: 'veiculos',
  titulo: 'Veículos (frota, cor e assentos)',
  resumo:
    'O cadastro da frota do transporte: placa, modelo, a cor que pinta o desenho do carro e o layout de assentos usado para alocar pacientes.',
  grupo: 'cadastros',
  icone: Bus,
  rota: '/app/veiculos',
  publico: 'Quem cadastra a frota e monta os translados do transporte de pacientes',
  atualizadoEm: '2026-09-29',
  palavrasChave: [
    'veículo',
    'frota',
    'carro',
    'van',
    'ambulância',
    'micro-ônibus',
    'ônibus',
    'placa',
    'modelo',
    'fabricante',
    'cor',
    'paleta',
    'CRLV',
    'RENAVAM',
    'desenho',
    'ilustração',
    'Onix',
    'Polo',
    'Spin',
    'Sprinter',
    'Master',
    'adaptada',
    'cadeirante',
    'acessibilidade',
    'assento',
    'fileira',
    'layout',
    'planta',
    'vista de cima',
    'corredor',
    'porta corrediça',
    'volante',
    'motorista',
    'acompanhante',
    'bloqueado',
    'translado',
    'alocar paciente',
    'excluir',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'A frota do transporte',
      busca: 'frota veículo translado rota motorista assento alocar paciente para que serve',
      conteudo: (
        <>
          <P>
            Cada veículo da frota é cadastrado uma vez, com placa, modelo, cor e o{' '}
            <strong>layout de assentos</strong> — quantas fileiras ele tem e o que é cada lugar. É
            esse cadastro que o translado usa: a rota do dia leva um veículo e um motorista, e os
            pacientes são alocados nos assentos dele.
          </P>
          <P>
            Na tela, o veículo aparece como um <strong>desenho do próprio modelo</strong>, pintado
            com a cor cadastrada — na lista, no detalhe e na hora de alocar paciente. A ideia é a
            equipe reconhecer o carro de olho, do mesmo jeito que reconhece na garagem: “a Spin
            cinza”, “a Sprinter azul”.
          </P>
        </>
      ),
    },
    {
      id: 'cadastrar',
      titulo: 'Cadastrar um veículo',
      busca: 'novo veículo cadastrar placa tipo fabricante modelo cor layout fileiras salvar',
      conteudo: (
        <>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    Em <strong>Cadastros → Veículos</strong>, clique em{' '}
                    <BotaoRef>Novo veículo</BotaoRef>.
                  </>
                ),
              },
              {
                titulo: 'Digite a placa.',
                detalhe:
                  'Vira maiúsculas sozinha. Confira bem: depois de cadastrada, a placa não se edita.',
              },
              {
                titulo: 'Escolha o tipo: Carro, Van, Micro-ônibus, Ônibus, Ambulância ou Outro.',
              },
              {
                titulo: 'Preencha fabricante e modelo com o nome comercial.',
                detalhe:
                  '"Sprinter", "Master", "Spin" — é pelo nome do modelo que o sistema escolhe o desenho. Se o veículo é adaptado para cadeirante, pode escrever junto: "Master adaptada".',
              },
              {
                titulo: 'Escolha a cor na paleta (ou escreva).',
                detalhe: 'A pré-visualização ao lado mostra o carro pintado enquanto você escolhe.',
              },
              {
                titulo: 'Monte o layout de assentos e clique em Cadastrar.',
                detalhe: 'Ver “O layout de assentos”, logo abaixo.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="Uma placa, um veículo — para sempre">
            O sistema recusa uma placa que já foi cadastrada, <strong>inclusive a de um veículo
            excluído</strong>. É o que garante que o histórico de translados de uma placa não se
            misture com o de outro carro.
          </Callout>
        </>
      ),
    },
    {
      id: 'cor',
      titulo: 'A cor: paleta e texto livre',
      busca: 'cor paleta crlv renavam documento branca prata preta cinza azul vermelha fantasia metálico marinho grafite escuro claro não reconhecida cinza neutro',
      conteudo: (
        <>
          <P>
            A paleta traz as 16 cores da tabela do RENAVAM — as mesmas que saem no documento do
            veículo (CRLV): Amarela, Azul, Bege, Branca, Cinza, Dourada, Grená, Laranja, Marrom,
            Prata, Preta, Rosa, Roxa, Verde, Vermelha e Fantasia (pintura de mais de uma cor ou
            adesivada). Clicar numa amostra grava o nome da cor.
          </P>
          <P>O campo de texto embaixo continua valendo, para quando a cor tem nome próprio:</P>
          <Lista>
            <Item>
              masculino ou feminino tanto faz: “Branco” e “Branca” pintam igual;
            </Item>
            <Item>
              tons conhecidos: “Azul marinho”, “Cinza grafite”, “Chumbo”, “Vinho”, “Champagne”;
            </Item>
            <Item>“escuro” e “claro” ajustam o tom: “Verde escuro”, “Azul claro”;</Item>
            <Item>“metálico” e “perolizado” são aceitos e ficam na cor-base.</Item>
          </Lista>
          <Callout tipo="atencao" titulo="Cor não reconhecida pinta de cinza">
            Se o texto não corresponde a nenhuma cor, o formulário avisa e o desenho fica cinza
            neutro. O cadastro salva do mesmo jeito — mas o carro deixa de ser reconhecível na tela.
            Prefira a paleta.
          </Callout>
        </>
      ),
    },
    {
      id: 'desenho',
      titulo: 'O desenho do carro',
      busca: 'desenho ilustração modelo onix polo spin sprinter master genérico sedan suv picape van micro-ônibus ônibus ambulância faixa cruz giroflex adaptada cadeirante selo acessibilidade',
      conteudo: (
        <>
          <P>
            Os modelos que a frota tem hoje têm <strong>desenho próprio</strong>, traçado sobre fotos
            de perfil e nas medidas reais de cada um: <strong>Onix, Polo, Spin, Sprinter e
            Master</strong>. Qualquer outro modelo recebe um desenho <strong>genérico</strong> da
            carroceria — sedan, SUV, picape, van, micro-ônibus ou ônibus —, escolhido pelo nome do
            modelo ou, sem pista no nome, pelo tipo cadastrado. O formulário diz qual foi usado:
            “Desenho do Renault Master” ou “Desenho genérico · SUV”.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Ambulância',
                descricao:
                  'Com o tipo Ambulância, o desenho ganha faixa lateral, cruz e giroflex no teto (faixa branca quando o carro é vermelho).',
              },
              {
                termo: 'Selo de cadeirante',
                descricao:
                  'Aparece quando o modelo diz que o veículo é adaptado (“ADAPTADA”, “acessível”, “PcD”) ou quando o layout tem assento de Cadeirante em uso. Na lista, que não carrega o layout, vale só o nome.',
              },
            ]}
          />
          <Sub>Experimente</Sub>
          <P>
            A simulação mostra o desenho de lado e a planta vista de cima, com um layout típico de
            cada modelo.
          </P>
          <SimulacaoDesenhoVeiculo />
        </>
      ),
    },
    {
      id: 'assentos',
      titulo: 'O layout de assentos',
      busca: 'layout assentos fileira F1 frente adicionar fileira excluir fileira passageiro motorista acompanhante cadeirante bloqueado alternar clicar limite 30 fileiras 10 assentos planta vista de cima corredor porta corrediça volante encosto proporção',
      conteudo: (
        <>
          <P>
            O layout aparece na <strong>planta do veículo</strong>: o carro visto de cima, com o
            teto tirado, na cor cadastrada e com a <strong>frente à direita</strong>. O lado de cima
            do desenho é o lado do motorista. Cada banco fica onde fica no veículo de verdade, nas
            medidas do modelo: <strong>F1</strong> é a fileira do motorista (com o volante à
            frente) e as seguintes vão para trás; o assento 1 de cada fileira é o do lado do
            motorista. A borda grossa de cada banco é o encosto.
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Carro (Onix, Polo, Spin…)',
                descricao:
                  'Dois bancos na frente com o console no meio; atrás, o banco inteiriço. A terceira fileira da Spin fica sobre o eixo traseiro.',
              },
              {
                termo: 'Van (Sprinter, Master)',
                descricao:
                  'Na cabine, motorista e banco duplo. Nas fileiras de trás, 3 assentos viram 2 + corredor + 1, com o corredor do lado da porta corrediça (a faixa tracejada na parede); 4 assentos ocupam a largura toda, como o último banco.',
              },
              {
                termo: 'Micro-ônibus e ônibus',
                descricao: 'Motorista sozinho na frente, porta à direita, e fileiras 2 + corredor + 2.',
              },
            ]}
          />
          <P>
            Cada fileira tem de 1 a 10 assentos, e o veículo tem de 1 a 30 fileiras. Se o layout
            tiver mais fileiras do que cabem no veículo, a planta aproxima as fileiras em vez de
            esconder alguma — é o aviso de que o cadastro está maior que o carro.
          </P>
          <P>
            Os botões − e + de cada fileira tiram e põem assentos;{' '}
            <BotaoRef variante="outline">Adicionar fileira</BotaoRef> cria uma nova no fim, com a
            mesma quantidade da última. Clicar num assento troca o tipo, nesta ordem:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Passageiro', descricao: 'Lugar comum para paciente.' },
              { termo: 'Motorista', descricao: 'Não recebe paciente na alocação.' },
              { termo: 'Acompanhante', descricao: 'Lugar reservado para quem vai junto com o paciente.' },
              { termo: 'Cadeirante', descricao: 'Posição de cadeira de rodas. Também faz aparecer o selo de acessibilidade no desenho.' },
              { termo: 'Bloqueado', descricao: 'Banco quebrado, lugar de equipamento etc. Não recebe paciente enquanto estiver bloqueado.' },
            ]}
          />
          <Callout tipo="dica" titulo="Mexer no layout não apaga o passado">
            Tirar um assento ou uma fileira de um veículo em uso só vale daqui para a frente: os
            translados que já usaram aquele lugar continuam registrados como estavam.
          </Callout>
        </>
      ),
    },
    {
      id: 'lista-detalhe',
      titulo: 'Lista e detalhe',
      busca: 'lista detalhe ver editar informações translados recentes status ativo',
      conteudo: (
        <>
          <P>
            A lista mostra os veículos ativos com placa, tipo, o desenho, fabricante e modelo e a
            cor (com uma bolinha na cor). Clicar na placa ou no desenho abre o detalhe: o layout de
            assentos na cor do carro, as informações com o desenho grande e os{' '}
            <strong>translados recentes</strong> daquele veículo.
          </P>
          <P>
            <BotaoRef variante="ghost">Editar</BotaoRef> abre o mesmo formulário do cadastro, com
            tudo preenchido — menos a placa, que fica travada.
          </P>
        </>
      ),
    },
    {
      id: 'no-translado',
      titulo: 'Na hora de alocar paciente',
      busca: 'translado alocar paciente assento livre sessão elegível mapa de assentos cor do veículo placa',
      conteudo: (
        <P>
          No detalhe do translado, o topo mostra o desenho do veículo da rota com a placa, e logo
          abaixo a planta dele, vista de cima, na cor do carro — para quem aloca conferir que está
          montando o veículo certo e escolher o lugar como escolheria olhando o carro. Ao escolher uma sessão elegível, os assentos livres piscam;
          os de <strong>Motorista</strong> e os bloqueados não aceitam paciente.
        </P>
      ),
    },
    {
      id: 'excluir',
      titulo: 'Excluir um veículo',
      busca: 'excluir desativar veículo vendido baixado histórico placa reservada reativar',
      conteudo: (
        <>
          <P>
            <BotaoRef variante="ghost">Excluir</BotaoRef> tira o veículo da lista e das opções de
            novos translados. Nada é apagado: os translados que ele fez continuam no histórico.
          </P>
          <Callout tipo="atencao" titulo="Não há reativar pela tela">
            A exclusão não se desfaz pela tela, e a placa continua reservada (não dá para
            cadastrá-la de novo). Se excluiu por engano, abra um ticket no Suporte.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil módulo veículos consulta inclusão edição exclusão',
      conteudo: (
        <>
          <P>
            O cadastro tem permissão própria, <strong>Veículos</strong>, na tela de Perfis:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Consulta', descricao: 'Ver a lista e o detalhe.' },
              { termo: 'Inclusão', descricao: 'Cadastrar veículo.' },
              { termo: 'Edição', descricao: 'Editar dados, cor e layout de assentos.' },
              { termo: 'Exclusão', descricao: 'Excluir.' },
            ]}
          />
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'desenho genérico modelo errado carro cinza cor selo cadeirante apareceu placa não edita',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'O desenho saiu genérico, mas o modelo é conhecido.',
              descricao:
                'Confira o nome do modelo: é por ele que o sistema acha o desenho ("Sprinter", não "Spr."). Se o modelo não está entre os que têm desenho próprio, peça pelo Suporte — cada modelo novo é desenhado a partir de fotos reais.',
            },
            {
              termo: 'O carro aparece cinza.',
              descricao: 'A cor escrita não foi reconhecida. Escolha a cor na paleta.',
            },
            {
              termo: 'Apareceu o selo de cadeirante e o carro não é adaptado.',
              descricao:
                'O layout tem assento do tipo Cadeirante. Troque o tipo do assento (clicando nele) ou bloqueie-o.',
            },
            {
              termo: 'Digitei a placa errada.',
              descricao:
                'A placa não se edita depois do cadastro. Abra um ticket no Suporte antes de cadastrar de novo — a placa errada fica reservada se o veículo for excluído.',
            },
          ]}
        />
      ),
    },
  ],
};

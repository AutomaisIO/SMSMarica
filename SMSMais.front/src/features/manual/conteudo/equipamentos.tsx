import { ScanLine } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo dos Equipamentos de imagem (`/app/equipamentos`, e a aba Equipamentos do detalhe da
 * unidade) — o aparelho que recebe a worklist.
 *
 * Conferido no código: `features/equipamentos` (EquipamentosPage, ModalEquipamento,
 * SelosWorklist), `unidades/components/EquipamentosDaUnidadeSecao` e no backend
 * (`EquipamentosService`, validadores, `ResolvedorEstacaoWorklist` — AE Title e escolha da sala —,
 * `ConstrutorMwlItem` — limite de descrição e código do procedimento, ADR-0074 —,
 * `EquipamentosController` para as permissões).
 */
export const artigoEquipamentos: Artigo = {
  slug: 'equipamentos',
  titulo: 'Equipamentos de imagem',
  resumo:
    'O cadastro de cada aparelho de imagem da unidade: o AE Title por onde a worklist chega, o que ele aguenta receber e, quando ele pede, o código do procedimento, o médico solicitante e a unidade.',
  grupo: 'assistencial',
  icone: ScanLine,
  rota: '/app/equipamentos',
  publico: 'Quem administra os aparelhos de imagem e a integração com o PACS',
  atualizadoEm: '2026-10-19',
  palavrasChave: [
    'equipamento',
    'equipamentos',
    'aparelho',
    'sala',
    'modalidade',
    'raio-x',
    'DX',
    'ultrassom',
    'mamógrafo',
    'ECG',
    'cardiologia',
    'Holter',
    'MAPA',
    'CardioNet',
    'OT',
    'densitometria',
    'aparelho de destino',
    'AE Title',
    'identificador DICOM',
    'worklist',
    'PACS',
    'limite da descrição',
    'Fuji',
    'código do procedimento',
    '0008,0100',
    'código interno',
    'protocolo',
    'fornecedor',
    'esquema',
    '99SMS',
    'envia código',
    'médico solicitante',
    'unidade solicitante',
    'unidade executante',
    'clínica',
    'tag DICOM',
    '0008,0090',
    '0008,0080',
    '0032,1033',
    'envia médico',
    'envia unidade',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Para que serve',
      busca: 'equipamento aparelho sala unidade worklist AE title destino exame imagem',
      conteudo: (
        <>
          <P>
            Cada aparelho de imagem de uma unidade — o raio-X, o ultrassom da sala 2, o mamógrafo —
            é um equipamento aqui. É por ele que o exame autorizado na recepção chega à{' '}
            <strong>worklist</strong> da máquina certa: o técnico abre a lista no aparelho e o
            paciente já está lá, com nome, nº do pedido e exame.
          </P>
          <P>
            A tela mostra os equipamentos de todas as unidades. O mesmo cadastro aparece na aba{' '}
            <strong>Equipamentos</strong> do detalhe de cada unidade, já filtrado.
          </P>
        </>
      ),
    },
    {
      id: 'os-campos',
      titulo: 'Os campos do cadastro',
      busca: 'nome unidade modalidade AE title identificador DICOM limite descrição ativo Fuji 16 64 ECG cardiologia Holter MAPA OT densitometria aparelho de destino escopo',
      conteudo: (
        <>
          <Lista>
            <Item>
              <strong>Nome</strong> — como a recepção vai reconhecer a sala (ex.: “Ultrassom Sala 2”).
              Não se repete na mesma unidade.
            </Item>
            <Item>
              <strong>Unidade</strong> e <strong>Modalidade</strong> — juntas, dizem quais exames podem
              ir para este aparelho. Quando a unidade tem mais de um aparelho da mesma modalidade, a
              recepção escolhe a sala ao autorizar — a não ser que o exame tenha{' '}
              <strong>aparelho de destino</strong> fixo na aba <em>Exames de imagem</em> da unidade. É o
              caso do CDT, onde densitometria e MAPA são os dois <strong>OT</strong>: cada um tem o seu
              aparelho fixo. Holter e ECG (cardiologia) usam a modalidade <strong>ECG</strong>. O
              aparelho e o tipo de exame precisam estar na mesma modalidade.
            </Item>
            <Item>
              <strong>AE Title</strong> — o nome configurado no próprio aparelho. A worklist só chega
              nesta máquina por ele. Até 16 caracteres, sem espaços nem acentos. Sem AE Title válido, o
              aparelho fica fora da worklist.
            </Item>
            <Item>
              <strong>Limite da descrição do exame</strong> — quantos caracteres do nome do exame o
              aparelho aguenta. Deixe 64, o teto do DICOM. Só baixe se o console deste aparelho falhar
              com nome longo — é o caso do mamógrafo Fuji, que fica em 16.
            </Item>
            <Item>
              <strong>Ativo</strong> — desmarcado, o aparelho sai da escolha da recepção e da
              worklist, mas o cadastro continua.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'o-que-vai-na-worklist',
      titulo: 'O que vai na worklist deste aparelho',
      busca:
        'campos opcionais tag DICOM médico solicitante unidade clínica executante solicitante origem do dado 0008,0090 0008,0080 0032,1033 envia médico envia unidade CardioNet técnico padrão',
      conteudo: (
        <>
          <P>
            Além do paciente e do exame, que vão sempre, cada aparelho pode receber três campos a mais:
            o <strong>código do procedimento</strong>, o <strong>médico solicitante</strong> e a{' '}
            <strong>unidade</strong> (a “clínica” de alguns consoles). Cada um tem a sua marcação e a sua{' '}
            <strong>tag DICOM</strong> — o campo do aparelho onde o dado entra.
          </P>
          <Lista>
            <Item>
              <strong>Médico solicitante</strong> — o nome de quem pediu o exame, como veio no pedido.
              Tag padrão <strong>0008,0090</strong> (Referring Physician's Name), a que o CardioNet da
              cardiologia do CDT lê.
            </Item>
            <Item>
              <strong>Unidade (clínica)</strong> — escolha a <strong>origem do dado</strong>: a unidade{' '}
              <strong>executante</strong> (onde o exame é feito) ou a <strong>solicitante</strong> (quem
              pediu). Tag padrão 0008,0080 para a executante e 0032,1033 para a solicitante.
            </Item>
            <Item>
              <strong>Código do procedimento</strong> — ver a seção seguinte.
            </Item>
          </Lista>
          <P>
            A tag já vem com o padrão. Mude só se o técnico do aparelho pedir outra: o sistema aceita
            qualquer tag de texto do dicionário DICOM, mas recusa as que o item já usa (paciente, nº do
            pedido, descrição do exame, data agendada…) e não deixa dois campos na mesma tag.
          </P>
          <P>
            Na lista, o aparelho ganha um selo por campo ligado — <strong>envia médico (0008,0090)</strong>,{' '}
            <strong>envia unidade executante (0008,0080)</strong> — ao lado do AE Title.
          </P>
          <Callout tipo="atencao" titulo="Vale para os exames enviados depois">
            O que já está na lista do aparelho não muda ao salvar o cadastro. Para um exame que já foi,
            use <em>Reenviar worklist</em> na tela do exame. Desligado — o padrão —, o item sai exatamente
            como sempre saiu, e ligar num aparelho não muda nada nos outros.
          </Callout>
        </>
      ),
    },
    {
      id: 'codigo-procedimento',
      titulo: 'Código do procedimento na worklist',
      busca:
        'código do procedimento 0008,0100 0032,1064 0040,0008 código interno SISREG protocolo fornecedor esquema 99SMS envia código raio-x CDT lista do fornecedor sequência tag de texto',
      conteudo: (
        <>
          <P>
            Alguns aparelhos escolhem o protocolo sozinhos pelo código do procedimento que vem na
            worklist — em vez de o técnico procurar na tela qual é o exame. Para isso, o fornecedor
            cadastra no aparelho a lista de códigos, e o sistema passa a mandar, em cada exame, o{' '}
            <strong>código interno do SISREG</strong>.
          </P>
          <Sub>Como ligar</Sub>
          <Lista>
            <Item>
              Marque <strong>Código do procedimento</strong>.
            </Item>
            <Item>
              <strong>Tag DICOM do código</strong> — no padrão, <strong>0008,0100</strong>, o código vai
              dentro de uma sequência de código, com esquema e descrição. Se o aparelho lê o código num
              campo de texto comum, ponha a tag desse campo: o código vai sozinho nela.
            </Item>
            <Item>
              Com a tag no padrão, escolha <strong>em que sequência</strong> o aparelho lê o código: a do
              procedimento solicitado (0032,1064) ou a do protocolo agendado (0040,0008). Ao ligar, as duas
              já vêm marcadas — mandar nas duas é inofensivo quando não se sabe qual o aparelho lê.
            </Item>
            <Item>
              <strong>Esquema</strong> (0008,0102) — deixe em branco (vale <em>99SMS</em>), a não ser
              que o fornecedor peça outro.
            </Item>
          </Lista>
          <Callout tipo="atencao" titulo="Ligue só no aparelho que foi cadastrado com os códigos">
            Ligar num aparelho que não conhece os códigos não quebra o exame, mas também não ajuda.
          </Callout>
          <Sub>O que vai em cada exame</Sub>
          <Lista>
            <Item>
              O código interno do procedimento <strong>daquele exame</strong>. No pedido com joelho
              direito e esquerdo, cada exame vai com o seu código (ver{' '}
              <em>Exames e consultas → Pedido com mais de um procedimento</em>).
            </Item>
            <Item>
              Na sequência, também a descrição do procedimento sem acento e no limite do aparelho. Nomes
              longos são abreviados antes de cortar (“RADIOGRAFIA” vira “RX”), para o direito e o esquerdo
              não ficarem iguais.
            </Item>
            <Item>
              Pedido lançado à mão, sem código do SISREG, usa o código do tipo de exame. Sem nenhum
              código, o exame vai sem ele e o técnico escolhe o protocolo como antes.
            </Item>
          </Lista>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil módulo equipamentos consulta inclusão edição exclusão',
      conteudo: (
        <P>
          Tudo aqui é governado pelo módulo <strong>Equipamentos</strong> do perfil: ver a lista é{' '}
          <strong>Consulta</strong>; cadastrar, <strong>Inclusão</strong>; mudar AE Title, limite e
          o que vai na worklist, <strong>Edição</strong>; excluir, <strong>Exclusão</strong>.
        </P>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca:
        'aparelho não recebe worklist protocolo não escolhido código não chega limite voltou 64 médico não aparece unidade clínica não aparece tag',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'O aparelho não recebe a worklist.',
              descricao:
                'Confira o AE Title: ele precisa ser idêntico ao configurado na máquina, e o equipamento precisa estar ativo, na unidade e na modalidade do exame. Depois, confira se a recepção autorizou o pedido — nada vai ao aparelho antes disso.',
            },
            {
              termo: 'Liguei o código e o aparelho não escolheu o protocolo.',
              descricao:
                'O aparelho só reconhece os códigos que o fornecedor cadastrou nele. Confira com o fornecedor a lista e a sequência em que ele lê o código; na dúvida, deixe as duas marcadas.',
            },
            {
              termo: 'Liguei o médico (ou a unidade) e não aparece no aparelho.',
              descricao:
                'Primeiro, o exame tem de ter sido enviado depois de salvar — para os que já estavam na lista, use Reenviar worklist. Se ainda assim não aparece, o aparelho lê outro campo: peça ao técnico a tag certa e troque no cadastro. Há aparelhos que simplesmente não pedem o campo (a “clínica” de alguns consoles é um cadastro local deles).',
            },
            {
              termo: 'Editei o equipamento e o limite da descrição mudou sozinho?',
              descricao:
                'Não muda mais: até outubro de 2026 a edição gravava 64 por cima do valor salvo. Hoje o que está gravado só muda se você alterar o campo.',
            },
          ]}
        />
      ),
    },
  ],
};

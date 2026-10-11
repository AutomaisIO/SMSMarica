import { Hospital } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo das Unidades de Atendimento — os destinos do Transporte de Pacientes — e do destino no
 * atendimento.
 *
 * Conferido no código: `features/unidades-atendimento` (lista, formulário, detalhe),
 * `features/tratamentos` (TratamentoFormPage, EditorDadosTratamento) e no backend
 * (`UnidadesAtendimentoService`, `SalvarUnidadeAtendimentoValidator`, `TratamentosService`
 * — GarantirDestinoAtivoAsync —, `GeradorDeTransladoService`).
 */
export const artigoUnidadesAtendimento: Artigo = {
  slug: 'unidades-atendimento',
  titulo: 'Unidades de Atendimento (destinos do transporte)',
  resumo:
    'O cadastro dos lugares onde o paciente é atendido fora da rede: o destino da van e o endereço que vai no aviso dos agendamentos do SER, do SERNIT e do ESUS.',
  grupo: 'transporte',
  icone: Hospital,
  rota: '/app/unidades-atendimento',
  publico: 'Quem organiza o transporte de pacientes e cadastra os atendimentos',
  atualizadoEm: '2026-10-10',
  palavrasChave: [
    'unidade de atendimento',
    'destino',
    'transporte',
    'TFD',
    'fora do município',
    'clínica',
    'hemodiálise',
    'hospital de referência',
    'endereço',
    'CEP',
    'mapa',
    'pin',
    'coordenada',
    'latitude',
    'longitude',
    'localizar pelo endereço',
    'sem coordenada',
    'rota',
    'van',
    'motorista',
    'atendimento',
    'tratamento',
    'desativar',
    'reativar',
    'maiúsculas',
    'locais dos agendamentos externos',
    'SER',
    'SERNIT',
    'ESUS',
    'endereço do aviso',
    'ligar local',
    'nome no sistema de regulação',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'O destino da van',
      busca: 'destino transporte unidade de atendimento diferença cadastro unidades sisreg equipamentos login perfil',
      conteudo: (
        <>
          <P>
            Unidade de atendimento é o lugar onde o paciente faz o tratamento e para onde a van o
            leva: a clínica de hemodiálise, o hospital de referência, o centro de oncologia. O
            endereço e o <strong>ponto no mapa</strong> dela são o fim da rota que o sistema calcula
            para cada carro.
          </P>
          <P>
            Parece com <strong>Cadastros → Unidades</strong>, mas serve a outra coisa. Aquelas são as
            unidades de saúde da rede, com CNES, SISREG, equipamentos, usuários que entram por elas e
            perfis. A unidade de atendimento não tem nada disso: é só o destino, cadastrado à mão por
            quem organiza o transporte. Por isso ela não aparece na escolha de unidade do login nem
            na atribuição de perfis.
          </P>
          <Callout tipo="dica" titulo="Cadastre antes do atendimento">
            O atendimento pede a unidade de atendimento. Se o destino ainda não existe, cadastre-o
            primeiro — o formulário do atendimento traz um atalho quando a lista está vazia.
          </Callout>
        </>
      ),
    },
    {
      id: 'cadastrar',
      titulo: 'Cadastrar uma unidade de atendimento',
      busca: 'nova unidade cadastrar cep buscar localizar pelo endereço arrastar pin porta fora do município observações motorista maiúsculas',
      conteudo: (
        <>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    Em <strong>Transporte Pacientes → Unidades de Atendimento</strong>, clique em{' '}
                    <BotaoRef>Nova unidade de atendimento</BotaoRef>.
                  </>
                ),
              },
              {
                titulo: 'Escreva o nome como a equipe chama o lugar.',
                detalhe: 'O campo vira MAIÚSCULAS sozinho — é o padrão desse cadastro.',
              },
              {
                titulo: (
                  <>
                    Digite o CEP e clique em <BotaoRef variante="outline">Buscar</BotaoRef>.
                  </>
                ),
                detalhe: 'Rua, bairro, cidade e UF vêm preenchidos. Complete o número e confira.',
              },
              {
                titulo: (
                  <>
                    Clique em <BotaoRef variante="outline">Localizar pelo endereço</BotaoRef> e confira
                    onde o pin caiu.
                  </>
                ),
                detalhe:
                  'Arraste o pin até a porta por onde o paciente entra (ou clique direto no mapa). A busca do mapa acerta a rua, nem sempre a entrada.',
              },
              {
                titulo: 'Confira "Fora do município (TFD)".',
                detalhe: 'Vem marcado, porque é o caso comum do transporte. Desmarque só se o destino fica no próprio município.',
              },
              {
                titulo: 'Se houver, escreva as observações para o motorista e salve.',
                detalhe: 'Portão, acesso de ambulância, onde o paciente desembarca, horário de funcionamento.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="Sem ponto no mapa, não salva">
            Logradouro, bairro, cidade, UF e o ponto no mapa são obrigatórios. Sem o ponto, o
            sistema tenta achar o endereço sozinho; se não achar, recusa e pede que você marque no
            mapa. Um destino sem coordenada deixaria a rota sem lugar de chegada.
          </Callout>
          <P>
            Não dá para ter duas unidades ativas com o mesmo nome — maiúsculas, minúsculas e espaços
            a mais não contam como diferença. Isso evita que o mesmo lugar apareça duas vezes na
            lista do atendimento.
          </P>
        </>
      ),
    },
    {
      id: 'lista',
      titulo: 'Como ler a lista',
      busca: 'lista buscar mostrar desativadas sem coordenada no mapa atendimentos ativos fora do município',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              { termo: 'Unidade', descricao: 'Nome e, embaixo, rua, número e bairro.' },
              {
                termo: 'Cidade/UF',
                descricao: (
                  <>
                    Com o selo <SeloRef>Fora do município</SeloRef> quando o destino é TFD.
                  </>
                ),
              },
              {
                termo: 'Localização',
                descricao: (
                  <>
                    “No mapa” quando está tudo certo. <SeloRef cor="alerta">Sem coordenada</SeloRef>{' '}
                    só aparece em registro antigo: edite e marque o ponto, senão a rota não tem
                    destino.
                  </>
                ),
              },
              {
                termo: 'Atendimentos ativos',
                descricao: 'Quantos atendimentos em andamento vão para lá.',
              },
            ]}
          />
          <P>
            A busca procura em nome, bairro, cidade e rua, sem ligar para acento. Por padrão só as
            ativas aparecem; marque <strong>Mostrar desativadas</strong> para ver as outras. Clicar
            na linha abre o detalhe: endereço, o mapa e a lista dos atendimentos com destino ali.
          </P>
        </>
      ),
    },
    {
      id: 'locais-externos',
      titulo: 'Locais dos agendamentos do SER, do SERNIT e do ESUS',
      busca:
        'locais agendamentos externos ser sernit esus endereço do aviso ligar desfazer sugestão parece conferi é esta sem endereço nome como o sistema escreve whatsapp paciente',
      conteudo: (
        <>
          <P>
            Quando um pedido do SER, do SERNIT ou do ESUS é agendado, o paciente recebe pelo WhatsApp o
            procedimento, o dia, a hora e o local. Esses sistemas mandam só o <strong>nome</strong> do local
            (“MS HGB HOSPITAL GERAL DE BONSUCESSO”) — o <strong>endereço</strong> sai daqui: da unidade de
            atendimento ligada a esse nome.
          </P>
          <P>
            A lista, embaixo das unidades, mostra cada local dos agendamentos futuros como o sistema o escreve,
            quantos agendamentos usam o nome e o próximo dia. Os que estão saindo <strong>sem endereço</strong>{' '}
            vêm primeiro.
          </P>
          <Passos
            itens={[
              {
                titulo: 'Escolha a unidade em “Ligar a”',
                detalhe:
                  'A busca acha pelo começo das palavras, sem acento. Se a unidade ainda não existe, cadastre-a com o endereço antes.',
              },
              {
                titulo: 'Ou confirme a sugestão',
                detalhe:
                  'Quando a tela acha uma unidade parecida, mostra “Parece: …”. Confira o endereço e use Conferi, é esta.',
              },
              {
                titulo: 'Pronto — vale já',
                detalhe:
                  'O próximo paciente que pedir os dados desse local recebe o endereço. Desfazer volta o local para “sem ligação”.',
              },
            ]}
          />
          <Callout tipo="atencao" titulo="A sugestão não decide">
            Nomes parecidos enganam: “Rio Imagem Lagos”, em Casimiro de Abreu, se parece com o Rio Imagem do Rio de
            Janeiro. Ligar errado manda o paciente para outra cidade. Por isso nada é ligado sozinho — só quando
            alguém escolhe.
          </Callout>
          <P>
            Um nome aponta para uma unidade só; uma unidade pode ter vários nomes (o SER e o SERNIT costumam escrever
            o mesmo hospital de jeitos diferentes). Unidade ligada <strong>sem endereço</strong> cadastrado também
            faz o aviso sair só com o nome — a lista avisa. Ligar e desfazer pede a permissão de editar unidades de
            atendimento.
          </P>
        </>
      ),
    },
    {
      id: 'no-tratamento',
      titulo: 'No atendimento: o destino',
      busca: 'atendimento tratamento destino editar dados trocar destino próximas rotas tempo médio tipo',
      conteudo: (
        <>
          <P>
            Ao cadastrar um atendimento (<strong>Transporte Pacientes → Atendimentos</strong>), a{' '}
            <strong>unidade de atendimento</strong> é obrigatória e é escolhida entre as ativas deste
            cadastro. É para onde a van vai.
          </P>
          <P>
            O tempo médio que o paciente fica lá não é da unidade nem do atendimento: é do{' '}
            <strong>tipo de tratamento</strong> (veja o artigo Tipos de tratamento e tempo médio).
          </P>
          <Sub>Mudar depois</Sub>
          <P>
            No detalhe do atendimento, <BotaoRef variante="outline">Editar dados</BotaoRef> troca o
            destino. A troca vale para as <strong>próximas</strong> rotas geradas; uma rota já montada
            não se refaz sozinha.
          </P>
          <Callout tipo="atencao" titulo="Atendimento encerrado não se edita">
            Depois de encerrado, o atendimento fica como estava. Para um novo ciclo, cadastre um
            atendimento novo.
          </Callout>
        </>
      ),
    },
    {
      id: 'desativar',
      titulo: 'Desativar e reativar',
      busca: 'desativar excluir reativar atendimento ativo recusado histórico',
      conteudo: (
        <>
          <P>
            <BotaoRef variante="ghost">Desativar</BotaoRef> tira a unidade das opções de destino do
            atendimento. Nada é apagado: os atendimentos antigos continuam mostrando o destino que
            tinham, e <BotaoRef variante="ghost">Reativar</BotaoRef> devolve a unidade à lista.
          </P>
          <Callout tipo="regra" titulo="Com atendimento ativo, não desativa">
            Enquanto houver atendimento em andamento indo para a unidade, o botão fica apagado. Sem
            essa trava, a rota continuaria indo a um destino que ninguém mais enxerga. Encerre os
            atendimentos ou troque o destino deles antes.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil módulo unidades de atendimento atendimentos consulta inclusão edição exclusão',
      conteudo: (
        <>
          <P>
            O cadastro tem permissão própria, <strong>Unidades de atendimento</strong>, na tela de
            Perfis:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Consulta', descricao: 'Ver a lista, o mapa e os atendimentos de cada unidade.' },
              { termo: 'Inclusão', descricao: 'Cadastrar unidade de atendimento.' },
              { termo: 'Edição', descricao: 'Editar endereço e ponto no mapa; reativar.' },
              { termo: 'Exclusão', descricao: 'Desativar.' },
            ]}
          />
          <P>
            Quem só cadastra atendimento <strong>não precisa</strong> dessa permissão: a lista de
            destinos do formulário vem junto com a permissão de Atendimentos do transporte.
          </P>
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'não aparece no atendimento localizar errado pin errado nome maiúsculas cadastros unidades',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'A unidade não aparece na lista do atendimento.',
              descricao:
                'Ela está desativada. Abra Unidades de Atendimento, marque "Mostrar desativadas" e reative.',
            },
            {
              termo: '"Localizar pelo endereço" pôs o pin no lugar errado.',
              descricao:
                'A busca do mapa é aproximada. Arraste o pin até a entrada certa — é esse ponto que vale para a rota.',
            },
            {
              termo: 'Por que o nome ficou todo em maiúsculas?',
              descricao: 'É o padrão do cadastro: o sistema grava sempre em maiúsculas, digitado de qualquer jeito.',
            },
            {
              termo: 'A clínica não aparece em Cadastros → Unidades.',
              descricao:
                'Está certo: aquele cadastro é das unidades de saúde da rede. Destino do transporte fica só aqui.',
            },
          ]}
        />
      ),
    },
  ],
};

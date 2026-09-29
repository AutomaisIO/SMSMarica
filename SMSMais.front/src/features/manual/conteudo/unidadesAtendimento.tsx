import { Hospital } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo das Unidades de Atendimento — os destinos do Transporte de Pacientes — e do que elas
 * mudaram no tratamento (destino + tempo médio).
 *
 * Conferido no código: `features/unidades-atendimento` (lista, formulário, detalhe),
 * `features/tratamentos` (TratamentoFormPage, EditorDadosTratamento, CampoTempoMedio) e no backend
 * (`UnidadesAtendimentoService`, `SalvarUnidadeAtendimentoValidator`, `TratamentosService`
 * — GarantirDestinoAtivoAsync —, `GeradorDeTransladoService`).
 */
export const artigoUnidadesAtendimento: Artigo = {
  slug: 'unidades-atendimento',
  titulo: 'Unidades de Atendimento (destinos do transporte)',
  resumo:
    'O cadastro dos lugares para onde a van leva o paciente: nome, endereço e o ponto no mapa que fecha a rota — e o tempo médio que o paciente fica lá.',
  grupo: 'transporte',
  icone: Hospital,
  rota: '/app/unidades-atendimento',
  publico: 'Quem organiza o transporte de pacientes e cadastra os tratamentos',
  atualizadoEm: '2026-09-29',
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
    'tempo médio',
    'duração do tratamento',
    'tratamento',
    'desativar',
    'reativar',
    'maiúsculas',
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
          <Callout tipo="dica" titulo="Cadastre antes do tratamento">
            O tratamento pede a unidade de atendimento. Se o destino ainda não existe, cadastre-o
            primeiro — o formulário do tratamento traz um atalho quando a lista está vazia.
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
            lista do tratamento.
          </P>
        </>
      ),
    },
    {
      id: 'lista',
      titulo: 'Como ler a lista',
      busca: 'lista buscar mostrar desativadas sem coordenada no mapa tratamentos ativos fora do município',
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
                termo: 'Tratamentos ativos',
                descricao: 'Quantos tratamentos em andamento vão para lá.',
              },
            ]}
          />
          <P>
            A busca procura em nome, bairro, cidade e rua, sem ligar para acento. Por padrão só as
            ativas aparecem; marque <strong>Mostrar desativadas</strong> para ver as outras. Clicar
            na linha abre o detalhe: endereço, o mapa e a lista dos tratamentos com destino ali.
          </P>
        </>
      ),
    },
    {
      id: 'no-tratamento',
      titulo: 'No tratamento: destino e tempo médio',
      busca: 'tratamento destino tempo médio horas minutos editar dados trocar destino próximas rotas volta retorno',
      conteudo: (
        <>
          <P>
            Ao cadastrar um tratamento, dois campos passaram a ser obrigatórios na etapa de dados:
          </P>
          <Lista>
            <Item>
              <strong>Unidade de atendimento (destino)</strong> — escolhida entre as ativas deste
              cadastro. É para onde a van vai.
            </Item>
            <Item>
              <strong>Tempo médio no tratamento</strong> — em horas e minutos, da chegada à
              liberação do paciente (ex.: hemodiálise ≈ 4h00). É a base para prever a volta no
              cálculo da rota. Aceita de 1 minuto a 24 horas.
            </Item>
          </Lista>
          <Sub>Mudar depois</Sub>
          <P>
            No detalhe do tratamento, <BotaoRef variante="outline">Editar dados</BotaoRef> troca o
            destino, o tempo médio e os dados gerais. A troca de destino vale para as{' '}
            <strong>próximas</strong> rotas geradas; uma rota já montada não se refaz sozinha.
          </P>
          <Callout tipo="atencao" titulo="Tratamento encerrado não se edita">
            Depois de encerrado, o tratamento fica como estava. Para um novo ciclo, cadastre um
            tratamento novo.
          </Callout>
        </>
      ),
    },
    {
      id: 'desativar',
      titulo: 'Desativar e reativar',
      busca: 'desativar excluir reativar tratamento ativo recusado histórico',
      conteudo: (
        <>
          <P>
            <BotaoRef variante="ghost">Desativar</BotaoRef> tira a unidade das opções de destino do
            tratamento. Nada é apagado: os tratamentos antigos continuam mostrando o destino que
            tinham, e <BotaoRef variante="ghost">Reativar</BotaoRef> devolve a unidade à lista.
          </P>
          <Callout tipo="regra" titulo="Com tratamento ativo, não desativa">
            Enquanto houver tratamento em andamento indo para a unidade, o botão fica apagado. Sem
            essa trava, a rota continuaria indo a um destino que ninguém mais enxerga. Encerre os
            tratamentos ou troque o destino deles antes.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil módulo unidades de atendimento tratamentos consulta inclusão edição exclusão',
      conteudo: (
        <>
          <P>
            O cadastro tem permissão própria, <strong>Unidades de atendimento</strong>, na tela de
            Perfis:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Consulta', descricao: 'Ver a lista, o mapa e os tratamentos de cada unidade.' },
              { termo: 'Inclusão', descricao: 'Cadastrar unidade de atendimento.' },
              { termo: 'Edição', descricao: 'Editar endereço e ponto no mapa; reativar.' },
              { termo: 'Exclusão', descricao: 'Desativar.' },
            ]}
          />
          <P>
            Quem só cadastra tratamento <strong>não precisa</strong> dessa permissão: a lista de
            destinos do formulário vem junto com a permissão de Tratamentos.
          </P>
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'não aparece no tratamento localizar errado pin errado nome maiúsculas cadastros unidades',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'A unidade não aparece na lista do tratamento.',
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

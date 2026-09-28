import { Headset } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { Item, Lista, ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { AbaRef, BotaoRef } from '@/features/manual/components/Referencia';
import { Passos } from '@/features/manual/components/Passos';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo do Softphone — o telefone dentro do painel. Duas pessoas leem: quem usa (o atendente,
 * que só precisa saber onde fica o botão e o que fazer quando não registra) e quem habilita
 * (o administrador, na edição do usuário). A ordem segue essa divisão.
 *
 * Sem rota própria: o softphone não é uma tela, é um botão flutuante presente em todas.
 */
export const artigoSoftphone: Artigo = {
  slug: 'softphone',
  titulo: 'Softphone',
  resumo:
    'O telefone dentro do painel: ligar e atender pelo navegador com um ramal próprio, sem aparelho e sem digitar senha.',
  grupo: 'atendimento',
  icone: Headset,
  publico: 'Quem liga e atende pelo painel, e o administrador que habilita o ramal de cada usuário',
  atualizadoEm: '2026-09-23',
  palavrasChave: [
    'softphone',
    'telefone',
    'ramal',
    'ligação',
    'ligar',
    'atender',
    'chamada',
    'microfone',
    'fone de ouvido',
    'headset',
    'telefonia',
    'mudo',
    'espera',
    'teclado',
    'outra aba',
    'não registra',
    'habilitar softphone',
    'nome de exibição',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'O que é o softphone',
      busca: 'o que é softphone telefone no navegador ramal sem aparelho',
      conteudo: (
        <div className="space-y-4">
          <P>
            O softphone é um telefone que funciona <strong>dentro do painel</strong>, no navegador. Cada usuário
            habilitado tem um <strong>ramal próprio</strong>: liga para outros ramais e recebe ligações sem precisar
            de aparelho na mesa — basta um fone de ouvido com microfone.
          </P>
          <P>
            Ele aparece como um <strong>botão redondo com um telefone</strong> no canto inferior direito da tela, ao
            lado do botão do chat. A bolinha colorida no botão diz se o ramal está pronto.
          </P>
          <Callout tipo="dica" titulo="Não tem senha para digitar">
            O ramal se conecta sozinho quando você entra no painel. A credencial do telefone é outra, gerada pelo
            sistema e entregue só ao seu navegador — por isso trocar a senha do painel não afeta o telefone.
          </Callout>
        </div>
      ),
    },
    {
      id: 'estados',
      titulo: 'O que a bolinha quer dizer',
      busca: 'status verde amarelo vermelho cinza pronto conectando sem registro outra aba',
      conteudo: (
        <ListaDefinicoes
          itens={[
            { termo: 'Verde — Pronto para ligar', descricao: 'O ramal está registrado: você liga e recebe ligações.' },
            {
              termo: 'Amarelo — Conectando',
              descricao:
                'O painel está falando com a telefonia. Se a conexão cair, ele tenta de novo sozinho; não precisa recarregar.',
            },
            {
              termo: 'Vermelho — Sem registro',
              descricao:
                'A telefonia recusou ou não respondeu. Recarregue a página; se continuar, abra um chamado no Suporte.',
            },
            {
              termo: 'Cinza — Ativo em outra aba',
              descricao:
                'O ramal só funciona em uma aba do painel por vez (com duas, a ligação tocaria nas duas). Use a aba onde ele está, ou feche-a — esta assume sozinha.',
            },
          ]}
        />
      ),
    },
    {
      id: 'ligar-e-atender',
      titulo: 'Ligar e atender',
      busca: 'fazer ligação discar número atender recusar desligar mudo espera teclado dtmf ura',
      conteudo: (
        <div className="space-y-4">
          <Sub>Para ligar</Sub>
          <Passos
            itens={[
              { titulo: 'Clique no botão redondo do telefone, no canto da tela.' },
              { titulo: 'Digite o ramal ou o número (pelo teclado da tela ou do computador).' },
              {
                titulo: (
                  <>
                    Clique em <BotaoRef>Ligar</BotaoRef>.
                  </>
                ),
                detalhe: 'Na primeira ligação o navegador pede permissão para usar o microfone — permita.',
              },
            ]}
          />
          <Sub>Quando alguém liga para você</Sub>
          <P>
            O painel abre sozinho, mostra quem está ligando e toca um bipe. Atenda pelo botão verde ou recuse pelo
            vermelho. Se você já estiver numa ligação, a segunda recebe sinal de ocupado — a que está em curso não é
            interrompida.
          </P>
          <Sub>Durante a ligação</Sub>
          <Lista>
            <Item>
              <strong>Microfone</strong> — silencia você; a outra pessoa continua sendo ouvida.
            </Item>
            <Item>
              <strong>Pausa</strong> — põe a ligação em espera; clique de novo para retomar.
            </Item>
            <Item>
              <strong>Teclado</strong> — para digitar opções de atendimento automático ("tecle 1…").
            </Item>
          </Lista>
        </div>
      ),
    },
    {
      id: 'microfone',
      titulo: 'Microfone e fone de ouvido',
      busca: 'testar microfone permissão navegador cadeado não ouve não escuta',
      conteudo: (
        <div className="space-y-4">
          <P>
            Em <strong>Meu perfil</strong> existe o cartão <strong>Softphone</strong>, com o seu ramal e o botão{' '}
            <BotaoRef variante="outline">Testar microfone</BotaoRef>. Use-o antes da primeira ligação: se o navegador
            não liberar o microfone, a mensagem diz o que fazer.
          </P>
          <Callout tipo="atencao" titulo="O navegador bloqueou o microfone">
            Clique no cadeado ao lado do endereço do site, procure <em>Microfone</em> e escolha <em>Permitir</em>.
            Depois recarregue a página.
          </Callout>
        </div>
      ),
    },
    {
      id: 'habilitar',
      titulo: 'Habilitar o softphone de um usuário (administrador)',
      busca: 'habilitar softphone usuário aba softphone ramal nome de exibição faixa números livres remover desativar',
      conteudo: (
        <div className="space-y-4">
          <P>
            Quem habilita é o administrador, em <strong>Usuários</strong> → editar o usuário → aba{' '}
            <AbaRef>Softphone</AbaRef>. A aba só aparece na edição de um usuário já cadastrado e para quem tem o
            módulo <strong>Telefonia</strong>.
          </P>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    Clique em <BotaoRef variante="outline">Habilitar softphone</BotaoRef>.
                  </>
                ),
              },
              {
                titulo: 'Confira o ramal.',
                detalhe:
                  'O sistema já sugere o primeiro número livre da faixa de softphones; a lista mostra os outros livres.',
              },
              {
                titulo: 'Confira o nome de exibição.',
                detalhe: 'É o que aparece no visor de quem recebe a ligação. Vem preenchido com o nome do usuário.',
              },
              {
                titulo: (
                  <>
                    Clique em <BotaoRef>Habilitar</BotaoRef>.
                  </>
                ),
                detalhe:
                  'A mudança vai para a telefonia na hora, independente do "Salvar alterações" do formulário. O usuário passa a ver o softphone ao recarregar o painel.',
              },
            ]}
          />
          <ListaDefinicoes
            itens={[
              {
                termo: 'Desmarcar "Ativo"',
                descricao:
                  'O softphone para de funcionar, mas o número continua reservado para o usuário. Bom para afastamentos.',
              },
              {
                termo: (
                  <>
                    <BotaoRef variante="ghost">Remover softphone</BotaoRef>
                  </>
                ),
                descricao: 'O ramal deixa de existir e o número fica livre para outra pessoa. Ligação em curso cai.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="Um ramal, uma pessoa">
            Cada usuário tem no máximo um softphone, e cada número pertence a um usuário só. Se o número escolhido
            já estiver em uso — por outro usuário ou por um aparelho da rede — o sistema recusa e diz o motivo.
          </Callout>
        </div>
      ),
    },
    {
      id: 'permissoes',
      titulo: 'Quem pode o quê',
      busca: 'permissão módulo telefonia consulta edição exclusão',
      conteudo: (
        <div className="space-y-4">
          <P>
            <strong>Usar</strong> o próprio softphone não exige módulo nenhum: basta o administrador tê-lo habilitado.
            O módulo <strong>Telefonia</strong> é de quem administra:
          </P>
          <ListaDefinicoes
            itens={[
              { termo: 'Consulta', descricao: 'Ver a aba Softphone de um usuário e os números livres.' },
              { termo: 'Edição', descricao: 'Habilitar, escolher ramal e nome, ativar e desativar.' },
              { termo: 'Exclusão', descricao: 'Remover o softphone (libera o número).' },
            ]}
          />
        </div>
      ),
    },
  ],
};

"""Regressões da busca de controles em telas roláveis, sem aparelho conectado."""
import tempfile
import unittest
import xml.etree.ElementTree as ET

from smoke_android import Android


class BuscaRolavelTests(unittest.TestCase):
    def test_procura_botao_por_id_apos_rolar_cabecalho_alto(self):
        with tempfile.TemporaryDirectory() as artifacts:
            app = Android("adb", None, "app.uitests", artifacts)
            pagina = [0]
            gestos = []

            def nodes():
                # No topo, o botão está fora da tela e não aparece na hierarquia.
                if pagina[0] == 0:
                    return []
                return [ET.fromstring('<node package="app.uitests" resource-id="app.uitests:id/CompararHistorico" enabled="true" bounds="[0,100][300,160]" />')]

            def swipe(down=False):
                gestos.append(down)
                pagina[0] += 1

            app.nodes = nodes
            app.swipe = swipe
            found = app.find("CompararHistorico", scroll=True, timeout=1)
            self.assertEqual("app.uitests:id/CompararHistorico", found.get("resource-id"))
            self.assertEqual([False], gestos)

    def test_nao_toca_botao_desabilitado_antes_da_selecao(self):
        with tempfile.TemporaryDirectory() as artifacts:
            app = Android("adb", None, "app.uitests", artifacts)
            enabled = [False]

            def nodes():
                return [ET.fromstring(f'<node package="app.uitests" resource-id="CompararHistorico" enabled="{str(enabled[0]).lower()}" bounds="[0,100][300,160]" />')]

            app.nodes = nodes
            app.swipe = lambda down=False: enabled.__setitem__(0, True)
            self.assertEqual("true", app.find("CompararHistorico", scroll=True, timeout=1).get("enabled"))

    def test_retorna_ao_topo_na_direcao_solicitada(self):
        with tempfile.TemporaryDirectory() as artifacts:
            app = Android("adb", None, "app.uitests", artifacts)
            gestos = []
            app.nodes = lambda: [] if not gestos else [ET.fromstring('<node package="app.uitests" text="Cálculos salvos" bounds="[0,100][300,160]" />')]
            app.swipe = lambda down=False: gestos.append(down)
            app.find("Cálculos salvos", scroll=True, scroll_down=True, timeout=1)
            self.assertEqual([True], gestos)


if __name__ == "__main__":
    unittest.main()

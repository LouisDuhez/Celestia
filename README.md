# Celestia

**Celestia** est un prototype de jeu vidéo de puzzle et plateforme, fortement inspiré de la mécanique emblématique du jeu *FEZ*. 

## 👥 L'Équipe
* **Louis DUHEZ** : Développement de la mécanique principale (Caméra FEZ-like, reprojection de la profondeur, système d'escalade).
* **Jesse François** : Conception et développement du système de Magie et des sortilèges.
* **Théo De-Oliveira** : Conception et développement (Game Design / Level Design).

## 🎮 Le Concept
Le cœur du gameplay repose sur la manipulation de la perspective. Le joueur évolue dans un monde 3D entier, mais affiché au travers d'une caméra 2D orthographique. 
À tout moment, le joueur peut faire pivoter la caméra de 90 degrés. Cette rotation altère la perception de la profondeur et permet de créer de nouveaux chemins en alignant visuellement des plateformes qui sont physiquement très éloignées l'une de l'autre.

### Fonctionnalités techniques notables :
* **Mécanique de Caméra Orthographique Rotative** avec un système de "Depth Snapping" (reprojection du joueur sur les plateformes).
* **Ledge Grab** : Détection avancée des rebords pour l'escalade, quelle que soit l'orientation de la caméra.
* **Checkpoints & Respawn** : Système de zones de mort et de points de sauvegarde.
* **Technologie embarquée** : Contrôle externe des caméras implémenté via support physique *(Micro:bit & Chataigne)*.

---

## 📝 Post-Mortem : Intégration et Collaboration

La version actuelle de ce livrable met principalement en avant la mécanique de base de la caméra et des déplacements. Le **système de sorts**, développé par Jesse et présenté dans le cadre d'un autre projet en cours, n'est que partiellement présent ou désactivé dans cette version finale.

**Bilan technique de cette décision :**
La magie n'étant pas la mécanique fondamentale pour valider le concept "FEZ", et face à des délais serrés, nous avons décidé de ne pas forcer son intégration complète. En effet, l'importation du système a révélé d'importants conflits de scripts et mis en lumière des logiques de programmation fondamentalement différentes entre les modules. Adapter et fusionner proprement ces systèmes aurait été trop risqué pour la stabilité du livrable.

**Les leçons retenues (Ce que cela nous a appris) :**
Cette contrainte technique a été une immense source d'apprentissage. Nous avons compris de manière concrète que **collaborer sur un projet de cette ampleur ne s'improvise pas**.
Pour qu'une fusion de travaux (merge) se passe bien, il est impératif :
1. De réfléchir à une architecture de code commune bien en amont.
2. De définir des règles de nommage et d'indépendance des scripts (découplage).
3. De tester l'intégration en continu, et non à la toute fin du projet. 
La production d'un jeu est autant un défi de communication et d'organisation qu'un défi de programmation.

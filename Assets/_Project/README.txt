PATCH PARA EL PROYECTO UNITY: CHUNGUNGO VS PERROS

Incluye:
- Scripts/Health.cs: vida en decimal para permitir bajar de 3 a 2.4 HP (20% menos).
- Scripts/HUD.cs: muestra vida decimal sin decimales innecesarios.
- Scripts/Projectile.cs: reproduce efecto de partículas al impactar en un objeto con IDamageable.
- Scripts/DamageImpactVFX.cs: crea un efecto breve de partículas sin necesitar un prefab externo.
- Prefabs/Enemy.prefab: vida máxima ajustada de 3 a 2.4.

ANTES DE COPIAR:
1. Haz una copia de seguridad de tu proyecto.
2. Copia los archivos de Scripts sobre Assets/_Project/Scripts/.
3. Copia Enemy.prefab sobre Assets/_Project/Prefabs/Enemy.prefab.
4. Si Unity pregunta si reemplazar, confirma solo después de hacer la copia de seguridad.
5. Deja que Unity compile y revisa Console.

NOTA SOBRE MODELOS:
Este parche no reemplaza aún el modelo visual del personaje ni los enemigos. El ZIP recibido contiene modelos genéricos (Character, Crab, Bee, Enemy, Skull), pero no contiene un modelo de chungungo ni modelos de perros. Para cambiar la apariencia sin romper controles/animaciones, hay que importar los modelos 3D de chungungo y perros y reemplazar únicamente el objeto Visual dentro de los prefabs, conservando los objetos raíz, colliders, Rigidbody y scripts.

var ballX = 120;
var ballY = 100;

var ballVX = 3;
var ballVY = 3;

var paddleY = 100;
var aiPaddleY = 100;     
 
const BGCOLOR = 12;


var score1 = 0;
var score2 = 0;

// Set the shared background color.
backdrop(BGCOLOR);

// Set background palette 0's three additional colors.
backgroundPalette(0, 5, 0, 0);

// Set sprite palette 0's three visible colors.
spritePalette(0, 16, 27, 30);




for(var x = 0; x < 32; x = x + 1) {
	tileAt(x, 2, 128);
	tileAt(x, 26, 128);
}


while (true) { 

    // --------------------------------
    // BALL MOVEMENT
    // --------------------------------

    ballX = ballX + ballVX;
    ballY = ballY + ballVY;


    // --------------------------------
    // TOP / BOTTOM WALLS
    // --------------------------------

    if (ballY < 20) {
        ballY = 20;
        ballVY = 3;
    }

    if (ballY > 200) {
        ballY = 200;
        ballVY = 0 - 3;
    }


    // --------------------------------
    // PLAYER INPUT
    // --------------------------------

    if (UP1()) {
        if (paddleY > 30) {
            paddleY = paddleY - 5;
        }
    }

    if (DOWN1()) {
        if (paddleY < 185) {
            paddleY = paddleY + 5;
        }
    }


    // --------------------------------
    // SIMPLE AI
    // --------------------------------

    if (ballY < aiPaddleY) {
        if (aiPaddleY > 16) {
            aiPaddleY = aiPaddleY - 2;
        }
    }

    if (ballY > aiPaddleY) {
        if (aiPaddleY < 218) {
            aiPaddleY = aiPaddleY + 2;
        }
    }


    // --------------------------------
    // LEFT PADDLE COLLISION
    // --------------------------------

    if (ballX < 24) {
		if (ballY > paddleY - 12) {
            if (ballY < paddleY + 12) {
                ballX = 24;
                ballVX = 3;
            }
        }
    }


    // --------------------------------
    // RIGHT PADDLE COLLISION
    // --------------------------------

    if (ballX > 232) {
		if (ballY > aiPaddleY - 12) {
            if (ballY < aiPaddleY + 12) {
                ballX = 232;
                ballVX = 0 - 3;
            }
        }
    }


    // --------------------------------
    // RESET IF BALL MISSES A PADDLE
    // --------------------------------
	
    if (ballX > 248) {
		score1 = score1 + 1;
        ballX = 120;
        ballY = 100;
        ballVX = 0 - 3;
        ballVY = 3;
    }

    if (ballX < 5) {
		score2 = score2 + 1;
        ballX = 120;
        ballY = 100;
        ballVX = 3;
        ballVY = 3;
    }

	if (score1 > 9) {
		score1 = 0;
		score2 = 0;
	}

	if (score2 > 9) {
		score1 = 0;
		score2 = 0;
	}

	// --------------------------------
    // DRAW PLAYER PADDLE
    // --------------------------------

    sprite1x3(0, 16, paddleY - 8, 128, 128, 128);
    

    // --------------------------------
    // DRAW AI PADDLE
    // --------------------------------

    sprite1x3(3, 240, aiPaddleY - 8, 128, 128, 128);
    

    // --------------------------------
    // DRAW BALL
    // --------------------------------

    sprite2x2(6, ballX - 4, ballY - 4, 107, 108, 109, 110);

    // --------------------------------
    // SYNCHRONIZE WITH NEXT FRAME
    // --------------------------------
	sync();
    

	tileAt(15, 4, score1 + 16);
	tileAt(16, 4, 26);
	tileAt(17, 4, score2 + 16);
}
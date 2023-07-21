# git root
# because if I do it normally (dirname "$PWD") it could lead to unfortunate results if anyone dares to
# run the "clean" command
#

COMPILER=pdflatex
VIEW=xdg-open

BASE_DIR=$(git rev-parse --show-toplevel)

REPORT_DIR="$BASE_DIR"/informe
REPORT_FILE="$REPORT_DIR"/Report.tex
REPORT_COMPILED="$REPORT_DIR"/Report.pdf
PRESENTATION_DIR="$BASE_DIR"/presentación
PRESENTATION_FILE="$PRESENTATION_DIR"/main.tex
PRESENTATION_COMPILED="$PRESENTATION_DIR"/main.pdf



run() {
    # run the project

    # reuse the main script
    make --directory "$BASE_DIR" dev
    return 0;
}

clean() {
    # delete unnecesary (not tracked) files
    # XXX i'm not bold enough to delete dotfiles
    # XXX this gives a scary message if you execute it
    # XXX be careful running this outside the repository 
    
    # this is simple
    # since rm doesn't delete hidden directories and .git is (indeed) a hidden directory
    cd "$BASE_DIR"
    rm -rf "$BASE_DIR"/*
    # reset
    git reset --hard
    echo "the purge was a success"
    return 0;
}

report() {
    # compile report

    TIMESTAMP=$(stat "$REPORT_DIR" --print=%Y)
    
    TIMESTAMP_CACHE="$REPORT_FILE".timestamp
    OLD_TIMESTAMP=$(cat "$TIMESTAMP_CACHE")
    NO_FILE=$?

    if [ $TIMESTAMP == $OLD_TIMESTAMP ] && [ $NO_FILE == 0 ]; then
        echo "$REPORT_FILE have not changed since last check. Nothing to do!";
        return 0;
    fi

    cd $REPORT_DIR
    $COMPILER $REPORT_FILE -output-directory 
    cd -
    echo $TIMESTAMP > "$TIMESTAMP_CACHE"

    return 0;
}

slides() {
    # compile slides


    TIMESTAMP=$(stat "$PRESENTATION_DIR" --print=%Y)
    
    TIMESTAMP_CACHE="$PRESENTATION_FILE".timestamp
    OLD_TIMESTAMP=$(cat "$TIMESTAMP_CACHE")
    NO_FILE=$?

    if [ $TIMESTAMP == $OLD_TIMESTAMP ] && [ $NO_FILE == 0 ]; then
        echo "$PRESENTATION_FILE have not changed since last check. Nothing to do!"
        return 0;
    fi

    cd $PRESENTATION_DIR
    $COMPILER $PRESENTATION_FILE
    cd -
    echo $TIMESTAMP > "$TIMESTAMP_CACHE"

    return 0;
}

show_report() {
    report;
    if [ $# -gt 0 ]; then
        VIEW=$1
    fi

    $VIEW $REPORT_COMPILED
    return 0;
}

show_slides() {
    slides;
    if [ $# -gt 0 ]; then
        VIEW=$1
    fi

    $VIEW $PRESENTATION_COMPILED
    return 0;
}

# why not? why not override the help command?
help() {
    # gets the local functions
    # i could wipe the private (starting with (_)) functions
    # to improve it but we didn't use them this time
    CMDS=$(compgen -A function | tr "\n" "|")
    echo "Use: $0 [$CMDS]]"
    exit 1;
}

# what's this, you ask?
# I just named my functions as the commands so I don't have to make a switch
$1 $2

if [ $? != 0 ]
then
    help;
fi
